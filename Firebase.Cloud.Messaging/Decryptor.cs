using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Firebase.Cloud.Messaging.Utils;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.EC;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Security;

namespace Firebase.Cloud.Messaging
{
    class Decryptor
    {
        private ECPrivateKeyParameters privateKey;
        private ECPublicKeyParameters publicKey;
        private ECDomainParameters ecDomainParameters;
        private ECKeyGenerationParameters eckgparameters;
        private ECCurve ecCurve;
        private ECDomainParameters ecSpec;
        private readonly X9ECParameters ecParams = NistNamedCurves.GetByName("P-256");
        private readonly SecureRandom secureRandom = new SecureRandom();

        // CEK_INFO = "Content-Encoding: aesgcm" || 0x00
        private static readonly byte[] keyInfoParameter = Encoding.ASCII.GetBytes("Content-Encoding: aesgcm\0");

        // NONCE_INFO = "Content-Encoding: nonce" || 0x00
        private static readonly byte[] nonceInfoParameter = Encoding.ASCII.GetBytes("Content-Encoding: nonce\0");
        private static readonly byte[] authInfoParameter = Encoding.ASCII.GetBytes("Content-Encoding: auth\0");
        private static readonly byte[] keyLabel = Encoding.ASCII.GetBytes("P-256");

        private const int NonceBitSize = 128;
        private const int MacBitSize = 128;
        private const int SHA_256_LENGTH = 32;
        private const int KEY_LENGTH = 16;
        private const int NONCE_LENGTH = 12;
        private const int HEADER_RS = 4096;
        private const int TAG_LENGTH = 16;
        private const int CHUNK_SIZE = HEADER_RS + TAG_LENGTH;
        private const int PADSIZE = 2;

        public Decryptor()
        {
            CreateEC();

            (privateKey, publicKey) = GenerateKeys();
            PublicKey = publicKey.Q.GetEncoded();

            AuthSecret = new byte[16];
            secureRandom.NextBytes(AuthSecret);
        }

        internal Decryptor(byte[] privateKeyBytes, byte[] publicKeyBytes, byte[] authSecret)
        {
            CreateEC();

            ECPoint pt = ecCurve.DecodePoint(publicKeyBytes);
            publicKey = new ECPublicKeyParameters(pt, ecDomainParameters);
            privateKey = new ECPrivateKeyParameters(new BigInteger(1, privateKeyBytes), ecDomainParameters);

            AuthSecret = authSecret;
            PublicKey = publicKey.Q.GetEncoded();
        }

        private void CreateEC()
        {
            ecCurve = ecParams.Curve;
            ecSpec = new ECDomainParameters(ecCurve, ecParams.G, ecParams.N, ecParams.H, ecParams.GetSeed());

            eckgparameters = new ECKeyGenerationParameters(ecSpec, secureRandom);
            ecDomainParameters = eckgparameters.DomainParameters;
        }

        public byte[] AuthSecret { get; }
        public byte[] PublicKey { get; }

        private (byte[], byte[]) ExtractDH(byte[] senderKey, byte[] receiverPrivateKey)
        {
            ECPoint pt = ecCurve.DecodePoint(senderKey);
            ECPublicKeyParameters publicKeyParams = new ECPublicKeyParameters(pt, ecDomainParameters);

            IBasicAgreement aKeyAgree = new ECDHBasicAgreement();
            aKeyAgree.Init(privateKey);
            byte[] sharedSecret = aKeyAgree.CalculateAgreement(publicKeyParams).ToByteArrayUnsigned();

            byte[] receiverKey = AddLengthPrefix(PublicKey);
            senderKey = AddLengthPrefix(senderKey);

            byte[] context = new byte[keyLabel.Length + 1 + receiverKey.Length + senderKey.Length];

            int destinationOffset = 0;
            Array.Copy(keyLabel, 0, context, destinationOffset, keyLabel.Length);
            destinationOffset += keyLabel.Length + 1;
            Array.Copy(receiverKey, 0, context, destinationOffset, receiverKey.Length);
            destinationOffset += receiverKey.Length;
            Array.Copy(senderKey, 0, context, destinationOffset, senderKey.Length);

            return (sharedSecret, context);
        }

        private byte[] AddLengthPrefix(byte[] buffer)
        {
            byte[] newBuffer = new byte[buffer.Length + 2];
            Array.Copy(buffer, 0, newBuffer, 2, buffer.Length);

            byte[] intBytes = BitConverter.GetBytes((short)buffer.Length);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(intBytes);
            }

            Debug.Assert(intBytes.Length <= 2);
            Array.Copy(intBytes, 0, newBuffer, 0, intBytes.Length);

            return newBuffer;
        }

        public byte[] Decrypt(byte[] buffer, byte[] senderPublicKeyBytes, byte[] salt)
        {
            var ecP = NistNamedCurves.GetByName("P-256");
            ECDomainParameters eCDomainParameters = new ECDomainParameters(ecP.Curve, ecP.G, ecP.N);

            ECPoint pt = ecP.Curve.DecodePoint(senderPublicKeyBytes);
            ECPublicKeyParameters senderPublicKey = new ECPublicKeyParameters(pt, eCDomainParameters);

            var (key, nonce) = DeriveKeyAndNonce(salt, AuthSecret, senderPublicKey, publicKey, privateKey);

            byte[] result = new byte[0];
            var start = 0;

            // TODO: this is not tested with more than one iteration
            for (uint i = 0; start < buffer.Length; ++i)
            {
                var end = start + CHUNK_SIZE;
                if (end == buffer.Length)
                {
                    throw new InvalidOperationException("Truncated payload");
                }

                end = Math.Min(end, buffer.Length);

                if (end - start <= TAG_LENGTH)
                {
                    throw new InvalidOperationException("Invalid block: too small at " + i);
                }

                byte[] block = DecryptRecord(key, nonce, i, ByteArray.Slice(buffer, start, end), end >= buffer.Length);
                result = ByteArray.Concat(result, block);
                start = end;
            }

            return result;
        }

        private byte[] RemovePad(byte[] buffer)
        {
            int pad = (int)ByteArray.ReadUInt64(buffer, 0, PADSIZE);

            if (pad + PADSIZE > buffer.Length)
            {
                throw new InvalidOperationException("padding exceeds block size");
            }

            return ByteArray.Slice(buffer, pad + PADSIZE, buffer.Length);
        }

        private byte[] DecryptRecord(byte[] key, byte[] nonce, uint counter, byte[] buffer, bool last)
        {
            nonce = GenerateNonce(nonce, counter);

            GcmBlockCipher blockCipher = new GcmBlockCipher(new AesEngine());

            byte[] tag = ByteArray.Slice(buffer, buffer.Length - TAG_LENGTH, buffer.Length);
            byte[] encryptedData = ByteArray.Slice(buffer, 0, buffer.Length - TAG_LENGTH);

            blockCipher.Init(false, new AeadParameters(new KeyParameter(key), 128, nonce));

            byte[] decryptedMessage = new byte[blockCipher.GetOutputSize(buffer.Length)];

            int decryptedMessageLength = blockCipher.ProcessBytes(encryptedData, 0, encryptedData.Length, decryptedMessage, 0);
            decryptedMessageLength += blockCipher.ProcessBytes(tag, 0, tag.Length, decryptedMessage, decryptedMessageLength);

            decryptedMessageLength += blockCipher.DoFinal(decryptedMessage, decryptedMessageLength);

            return RemovePad(decryptedMessage);
        }

        private (byte[], byte[]) DeriveKeyAndNonce(byte[] salt, byte[] authSecret, ECPublicKeyParameters senderPublicKey, ECPublicKeyParameters receiverPublicKey, ECPrivateKeyParameters receiverPrivateKey)
        {
            var (secret, context) = ExtractSecretAndContext(senderPublicKey, receiverPublicKey, receiverPrivateKey);
            secret = HKDF.GetBytes(authSecret, secret, authInfoParameter, SHA_256_LENGTH);

            byte[] keyInfo = ByteArray.Concat(keyInfoParameter, context);
            byte[] nonceInfo = ByteArray.Concat(nonceInfoParameter, context);

            byte[] prk = HKDF.Extract(salt, secret);

            return (HKDF.Expand(prk, keyInfo, KEY_LENGTH), HKDF.Expand(prk, nonceInfo, NONCE_LENGTH));
        }

        private (byte[], byte[]) ExtractSecretAndContext(ECPublicKeyParameters senderPublicKey, ECPublicKeyParameters receiverPublicKey, ECPrivateKeyParameters receiverPrivateKey)
        {
            IBasicAgreement aKeyAgree = new ECDHBasicAgreement();

            aKeyAgree.Init(receiverPrivateKey);
            byte[] sharedSecret = aKeyAgree.CalculateAgreement(senderPublicKey).ToByteArrayUnsigned();

            byte[] receiverKeyBytes = AddLengthPrefix(receiverPublicKey.Q.GetEncoded());
            byte[] senderPublicKeyBytes = AddLengthPrefix(senderPublicKey.Q.GetEncoded());

            byte[] context = new byte[keyLabel.Length + 1 + receiverKeyBytes.Length + senderPublicKeyBytes.Length];

            int destinationOffset = 0;
            Array.Copy(keyLabel, 0, context, destinationOffset, keyLabel.Length);
            destinationOffset += keyLabel.Length + 1;
            Array.Copy(receiverKeyBytes, 0, context, destinationOffset, receiverKeyBytes.Length);
            destinationOffset += receiverKeyBytes.Length;
            Array.Copy(senderPublicKeyBytes, 0, context, destinationOffset, senderPublicKeyBytes.Length);

            return (sharedSecret, context);
        }

        private byte[] GenerateNonce(byte[] buffer, uint counter)
        {
            byte[] nonce = new byte[buffer.Length];
            Buffer.BlockCopy(buffer, 0, nonce, 0, buffer.Length);
            ulong m = ByteArray.ReadUInt64(nonce, nonce.Length - 6, 6);
            ulong x = ((m ^ counter) & 0xffffff) + ((((m / 0x1000000) ^ (counter / 0x1000000)) & 0xffffff) * 0x1000000);
            ByteArray.WriteUInt64(nonce, m, nonce.Length - 6, 6);

            return nonce;
        }

        private (ECPrivateKeyParameters, ECPublicKeyParameters) GenerateKeys()
        {
            ECKeyPairGenerator gen = new ECKeyPairGenerator("ECDH");
            gen.Init(eckgparameters);
            AsymmetricCipherKeyPair eckp = gen.GenerateKeyPair();

            ECPublicKeyParameters ecPub = (ECPublicKeyParameters)eckp.Public;
            ECPrivateKeyParameters ecPri = (ECPrivateKeyParameters)eckp.Private;

            return (ecPri, ecPub);
        }

        private class HKDF
        {

            /// <summary>
            /// Returns a 32 byte psuedorandom number that can be used with the Expand method if 
            /// a cryptographically secure pseudorandom number is not already available.
            /// </summary>
            /// <param name="salt">(Optional, but you should use it) Non-secret random value. 
            /// If less than 64 bytes it is padded with zeros. Can be reused but output is 
            /// stronger if not reused. (And of course output is much stronger with salt than 
            /// without it)</param>
            /// <param name="inputKeyMaterial">Material that is not necessarily random that
            /// will be used with the HMACSHA256 hash function and the salt to produce
            /// a 32 byte psuedorandom number.</param>
            /// <returns></returns>
            public static byte[] Extract(byte[] salt, byte[] inputKeyMaterial)
            {
                //For algorithm docs, see section 2.2: https://tools.ietf.org/html/rfc5869 

                using (System.Security.Cryptography.HMACSHA256 hmac = new System.Security.Cryptography.HMACSHA256(salt))
                {
                    return hmac.ComputeHash(inputKeyMaterial, offset: 0, count: inputKeyMaterial.Length);
                }
            }


            /// <summary>
            /// Returns a secure pseudorandom key of the desired length. Useful as a key derivation
            /// function to derive one cryptograpically secure pseudorandom key from another
            /// cryptograpically secure pseudorandom key. This can be useful, for example,
            /// when needing to create a subKey from a master key.
            /// </summary>
            /// <param name="key">A cryptograpically secure pseudorandom number. Can be obtained
            /// via the Extract method or elsewhere. Must be 32 bytes or greater. 64 bytes is 
            /// the prefered size.  Shorter keys are padded to 64 bytes, longer ones are hashed
            /// to 64 bytes.</param>
            /// <param name="info">(Optional) Context and application specific information.
            /// Allows the output to be bound to application context related information.</param>
            /// <param name="length">Length of output in bytes.</param>
            /// <returns></returns>
            public static byte[] Expand(byte[] key, byte[] info, int length)
            {
                //For algorithm docs, see section 2.3: https://tools.ietf.org/html/rfc5869 
                //Also note:
                //       SHA256 has a block size of 64 bytes
                //       SHA256 has an output length of 32 bytes (but can be truncated to less)

                const int hashLength = 32;

                //Min recommended length for Key is the size of the hash output (32 bytes in this case)
                //See section 2: https://tools.ietf.org/html/rfc2104#section-3
                //Also see:      http://security.stackexchange.com/questions/95972/what-are-requirements-for-hmac-secret-key
                if (key == null || key.Length < 32)
                {
                    throw new ArgumentOutOfRangeException("Key should be 32 bytes or greater.");
                }

                if (length > 255 * hashLength)
                {
                    throw new ArgumentOutOfRangeException("Output length must 8160 bytes or less which is 255 * the SHA256 block site of 32 bytes.");
                }

                int outputIndex = 0;
                byte[] buffer;
                byte[] hash = new byte[0];
                byte[] output = new byte[length];
                int count = 1;
                int bytesToCopy;

                using (System.Security.Cryptography.HMACSHA256 hmac = new System.Security.Cryptography.HMACSHA256(key))
                {
                    while (outputIndex < length)
                    {
                        //Setup buffer to hash
                        buffer = new byte[hash.Length + info.Length + 1];
                        Buffer.BlockCopy(hash, 0, buffer, 0, hash.Length);
                        Buffer.BlockCopy(info, 0, buffer, hash.Length, info.Length);
                        buffer[buffer.Length - 1] = (byte)count++;

                        //Hash the buffer and return a 32 byte hash
                        hash = hmac.ComputeHash(buffer, offset: 0, count: buffer.Length);

                        //Copy as much of the hash as we need to the final output
                        bytesToCopy = Math.Min(length - outputIndex, hash.Length);
                        Buffer.BlockCopy(hash, 0, output, outputIndex, bytesToCopy);
                        outputIndex += bytesToCopy;
                    }
                }

                return output;
            }


            /// <summary>
            /// Generates a psuedorandom number of the length specified.  This number is suitable
            /// for use as an encryption key, HMAC validation key or other uses of a cryptographically
            /// secure psuedorandom number.
            /// </summary>
            /// <param name="salt">non-secret random value. If less than 64 bytes it is 
            /// padded with zeros. Can be reused but output is stronger if not reused.</param>
            /// <param name="inputKeyMaterial">Material that is not necessarily random that
            /// will be used with the HMACSHA256 hash function and the salt to produce
            /// a 32 byte psuedorandom number.</param>
            /// <param name="info">(Optional) context and application specific information.
            /// Allows the output to be bound to application context related information. Pass 0 length
            /// byte array to omit.</param>
            /// <param name="length">Length of output in bytes.</param>
            public static byte[] GetBytes(byte[] salt, byte[] inputKeyMaterial, byte[] info, int length)
            {
                byte[] key = Extract(salt, inputKeyMaterial);
                return Expand(key, info, length);
            }
        }
    }
}
