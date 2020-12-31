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
        effect, //- ?? new 
        rt      //- receive effect in real time
    }
}


