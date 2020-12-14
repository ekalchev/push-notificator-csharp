using Firebase.Cloud.Messaging;
using NUnit.Framework;
using System.Text;

namespace Tests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Decrypt_EncyptedByteArrayFromFCMService_ShouldSuccessfullyDecrypt()
        {
            // use this captured data for debugging
            ////// generated on the client - ECDH with curve prime256v1//////
            // random bytes
            var authSecret = new byte[] { 5, 47, 48, 155, 244, 31, 204, 235, 11, 247, 67, 120, 24, 137, 25, 153 };

            //// public key sent to the server
            var receiverPublicKeyBytes = new byte[] { 4, 234, 243, 178, 1, 91, 224, 122, 211, 185, 63, 90, 135, 90, 206, 224, 43, 63, 63, 131, 227, 22, 157, 108, 31, 176, 83, 27, 70, 246, 89, 112, 7, 102, 79, 42, 205, 17, 100, 100, 149, 198, 135, 95, 241, 189, 182, 61, 103, 161, 4, 244, 127, 185, 128, 18, 139, 78, 3, 169, 111, 218, 80, 73, 55 };

            //// private key kept on the client
            var privateKey = new byte[] { 250, 117, 42, 156, 20, 153, 20, 193, 233, 136, 185, 246, 56, 52, 250, 150, 120, 250, 72, 147, 182, 144, 120, 103, 76, 11, 175, 143, 92, 1, 177, 59 };

            //// received from the server
            var salt = new byte[] { 248, 70, 134, 75, 160, 188, 58, 83, 105, 238, 59, 171, 27, 115, 224, 200 };

            //// server public key
            var senderPublicKeyBytes = new byte[] { 4, 26, 9, 166, 16, 222, 177, 154, 230, 15, 231, 11, 89, 108, 66, 97, 247, 3, 158, 199, 93, 98, 187, 162, 175, 76, 127, 2, 149, 67, 13, 195, 26, 145, 46, 223, 4, 34, 46, 70, 57, 0, 98, 139, 79, 25, 84, 187, 176, 126, 50, 108, 192, 61, 207, 83, 248, 189, 14, 10, 182, 18, 141, 52, 92 };

            //// actual data that needs decoding
            var rawData = new byte[] { 127, 5, 92, 210, 222, 94, 48, 180, 122, 71, 186, 120, 91, 171, 10, 6, 14, 182, 145, 108, 136, 161, 172, 8, 67, 27, 136, 55, 6, 224, 180, 181, 141, 242, 21, 101, 235, 6, 125, 162, 97, 236, 49, 150, 61, 225, 130, 58, 57, 93, 37, 79, 208, 21, 8, 139, 72, 235, 12, 173, 50 };

            var decryptor = new Decryptor(privateKey, receiverPublicKeyBytes, authSecret);
            var decryptedBytes = decryptor.Decrypt(rawData, senderPublicKeyBytes, salt);
            var result = Encoding.UTF8.GetString(decryptedBytes);

            Assert.AreEqual("{\"from\":\"550920961559\",\"priority\":\"normal\"}", result);
        }
    }
}