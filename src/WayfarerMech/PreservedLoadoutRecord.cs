using System;
using Newtonsoft.Json.Linq;

namespace WayfarerMech
{
    // Keep the complete record until the player explicitly applies a replacement.
    // A playable fallback must never become an implicit save-data migration.
    public sealed class PreservedLoadoutRecord
    {
        private readonly JToken payload;
        public int Version { get; }

        public PreservedLoadoutRecord(JToken payload, int version)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            this.payload = payload.DeepClone();
            Version = version;
        }

        public JToken Capture() => payload.DeepClone();
    }
}
