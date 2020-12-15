using System;
using System.Collections.Generic;
using System.Text;

namespace Firebase.Cloud.Messaging
{
    enum ProcessingState
    {
        MCS_VERSION_TAG_AND_SIZE,
        MCS_TAG_AND_SIZE,
        MCS_SIZE,
        MCS_PROTO_BYTES
    }
}
