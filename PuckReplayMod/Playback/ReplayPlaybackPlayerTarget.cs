namespace PuckReplayMod
{
    public struct ReplayPlaybackPlayerTarget
    {
        public ReplayPlaybackPlayerTarget(ulong ownerClientId, string displayName)
        {
            this.OwnerClientId = ownerClientId;
            this.DisplayName = displayName;
        }

        public ulong OwnerClientId;
        public string DisplayName;
    }
}
