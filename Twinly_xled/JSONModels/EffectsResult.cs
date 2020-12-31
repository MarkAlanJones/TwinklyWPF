namespace Twinkly_xled.JSONModels
{
    public class EffectsResult
    {
        public int effects_number { get; set; } // How many effects ?
        public string[] unique_ids { get; set; } // the ids - guid like with lots of zero
        public int code { get; set; }
    }

    public class EffectsCurrentResult
    {
        public int effect_id { get; set; } // the current effect (0 -> effect number)
        public string unique_id { get; set; } // looks Guid like with lots of 0
        public int code { get; set; }
    }

    public class MergedEffectsResult
    {
        public int effects_number { get; set; } // How many effects ?
        public int effect_id { get; set; } // the current effect (0 -> effect number)
        public string unique_id { get; set; }
        public string[] unique_ids { get; set; }
        public int code { get; set; }
    }

}
