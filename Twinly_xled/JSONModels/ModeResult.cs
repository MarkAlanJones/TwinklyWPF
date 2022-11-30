namespace Twinkly_xled.JSONModels
{
    public class Mode
    {
        public string mode { get; set; }
    }

    public class ModeResult : Mode
    {
        public int code { get; set; }
    }

    public enum LedModes
    {
        off,    //- turns off lights
        demo,   //- starts predefined sequence of effects that are changed after few seconds
        movie,  //- plays predefined or uploaded effect 
        effect, //- ?? new (plays effect effect_id)
        rt,     //- receive effect in real time UDP
        color,  // - shows a static color
        // playlist - since 2.5.6
    }
}


