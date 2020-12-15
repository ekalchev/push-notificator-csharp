using System;
using System.Collections.Generic;
using System.Text;

namespace Firebase.Cloud.Messaging
{
    enum MessageTag
    {
        kHeartbeatPingTag = 0,
        kHeartbeatAckTag = 1,
        kLoginRequestTag = 2,
        kLoginResponseTag = 3,
        kCloseTag = 4,
        kMessageStanzaTag = 5,
        kPresenceStanzaTag = 6,
        kIqStanzaTag = 7,
        kDataMessageStanzaTag = 8,
        kBatchPresenceStanzaTag = 9,
        kStreamErrorStanzaTag = 10,
        kHttpRequestTag = 11,
        kHttpResponseTag = 12,
        kBindAccountRequestTag = 13,
        kBindAccountResponseTag = 14,
        kTalkMetadataTag = 15,
        kNumProtoTypes = 16,
    }
}
