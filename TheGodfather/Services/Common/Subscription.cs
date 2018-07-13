﻿namespace TheGodfather.Services.Common
{
    public sealed class Subscription
    {
        public ulong ChannelId { get; private set; }
        public string QualifiedName { get; private set; }


        public Subscription(ulong cid, string qname)
        {
            this.ChannelId = cid;
            this.QualifiedName = qname;
        }
    }
}
