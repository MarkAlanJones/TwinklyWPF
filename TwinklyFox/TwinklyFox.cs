using ExtensionMethods;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing; // use drawing instead of meadia for named color ?
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography;
using System.Threading.Channels;

namespace Twinkly.Fox
{
    /// <summary>
    /// Arduino FastLED demo - converted to C# for Twinkly networked lights 
    /// From https://gist.github.com/kriegsman/756ea6dcae8e30845b5a
    /// Featured in HAD https://hackaday.com/2022/12/21/led-christmas-lights-optimized-for-max-twinkleage/
    /// Converted to C# my Mark Jones - Dec 23 2022
    /// </summary>
    public class TwinklyFox
    {
        public TwinklyFox(int num_leds, int bpl)
        {
            NUM_LEDS = num_leds;
            bytesperled = bpl;
            Leds = new byte[num_leds * bytesperled];
            gTargetPalette = ChooseNextColorPalette();
            gCurrentPalette = (Color[])gTargetPalette.Clone();

            pstopWatch = new Stopwatch();
            blendsw = new Stopwatch();
            fpsTimer = new Stopwatch();
        }

        private readonly Stopwatch pstopWatch;
        private readonly Stopwatch blendsw;
        private readonly Stopwatch fpsTimer;

        public int NUM_LEDS { get; private set; } = 100;
        private int bytesperled { get; set; } = 3;

        /// <summary>
        /// track how many times through the loop
        /// </summary>
        public int LoopCount { get; private set; } = 0;

        public double FPS { get; private set; } = 0;

        /// <summary>
        /// The Name of the current palette, not the values
        /// </summary>
        public string CurrentPalette { get; private set; } = string.Empty;

        //  TwinkleFOX: Twinkling 'holiday' lights that fade in and out.
        //  Colors are chosen from a palette; a few palettes are provided.
        //
        //  This December 2015 implementation improves on the December 2014 version
        //  in several ways:
        //  - smoother fading, compatible with any colors and any palettes
        //  - easier control of twinkle speed and twinkle density
        //  - supports an optional 'background color'
        //  - takes even less RAM: zero RAM overhead per pixel
        //  - illustrates a couple of interesting techniques (uh oh...)
        //
        //  The idea behind this (new) implementation is that there's one
        //  basic, repeating pattern that each pixel follows like a waveform:
        //  The brightness rises from 0..255 and then falls back down to 0.
        //  The brightness at any given point in time can be determined as
        //  as a function of time, for example:
        //    brightness = sine( time ); // a sine wave of brightness over time
        //
        //  So the way this implementation works is that every pixel follows
        //  the exact same wave function over time.  In this particular case,
        //  I chose a sawtooth triangle wave (triwave8) rather than a sine wave,
        //  but the idea is the same: brightness = triwave8( time ).  
        //  
        //  Of course, if all the pixels used the exact same wave form, and 
        //  if they all used the exact same 'clock' for their 'time base', all
        //  the pixels would brighten and dim at once -- which does not look
        //  like twinkling at all.
        //
        //  So to achieve random-looking twinkling, each pixel is given a 
        //  slightly different 'clock' signal.  Some of the clocks run faster, 
        //  some run slower, and each 'clock' also has a random offset from zero.
        //  The net result is that the 'clocks' for all the pixels are always out 
        //  of sync from each other, producing a nice random distribution
        //  of twinkles.
        //
        //  The 'clock speed adjustment' and 'time offset' for each pixel
        //  are generated randomly.  One (normal) approach to implementing that
        //  would be to randomly generate the clock parameters for each pixel 
        //  at startup, and store them in some arrays.  However, that consumes
        //  a great deal of precious RAM, and it turns out to be totally
        //  unnessary!  If the random number generate is 'seeded' with the
        //  same starting value every time, it will generate the same sequence
        //  of values every time.  So the clock adjustment parameters for each
        //  pixel are 'stored' in a pseudo-random number generator!  The PRNG 
        //  is reset, and then the first numbers out of it are the clock 
        //  adjustment parameters for the first pixel, the second numbers out
        //  of it are the parameters for the second pixel, and so on.
        //  In this way, we can 'store' a stable sequence of thousands of
        //  random clock adjustment parameters in literally two bytes of RAM.
        //
        //  There's a little bit of fixed-point math involved in applying the
        //  clock speed adjustments, which are expressed in eighths.  Each pixel's
        //  clock speed ranges from 8/8ths of the system clock (i.e. 1x) to
        //  23/8ths of the system clock (i.e. nearly 3x).
        //
        //  On a basic Arduino Uno or Leonardo, this code can twinkle 300+ pixels
        //  smoothly at over 50 updates per seond.
        //
        //  -Mark Kriegsman, December 2015

        // the realtime buffer has 1 FRAME  3 or 4 bytes for every light
        // header bytes are handled in DataAccess
        // This Frame is filled by the Loop Function, once the Loop has run, the frame can be displayed
        public byte[] Leds { get; private set; }

        #region User Tweakable Properties

        // Overall twinkle speed.
        // 0 (VERY slow) to 8 (VERY fast).  
        // 4, 5, and 6 are recommended, default is 4.
        public int TWINKLE_SPEED { get; set; } = 4;

        // Overall twinkle density.
        // 0 (NONE lit) to 8 (ALL lit at once).  
        // Default is 5.
        public int TWINKLE_DENSITY { get; set; } = 5;

        // How often to change color palettes.
        public int SECONDS_PER_PALETTE { get; set; } = 30;
        // Also: toward the bottom of the file is an array 
        // called "ActivePaletteList" which controls which color
        // palettes are used; you can add or remove color palettes
        // from there freely.

        // Background color for 'unlit' pixels
        // Can be set toColor.Black if desired.
        private Color gBackgroundColor = Color.Black;
        private static readonly Color FairyLightNCC = Color.FromArgb(0xFF, 0x9D, 0x2A);

        // Example of dim incandescent fairy light background color
        // CRGB gBackgroundColor = CRGB(CRGB::FairyLight).nscale8_video(16);

        // If AUTO_SELECT_BACKGROUND_COLOR is set to 1,
        // then for any palette where the first two entries 
        // are the same, a dimmed version of that color will
        // automatically be used as the background color.
        public bool AUTO_SELECT_BACKGROUND_COLOR { get; set; } = false;

        // If COOL_LIKE_INCANDESCENT is set to 1, colors will 
        // fade out slighted 'reddened', similar to how
        // incandescent bulbs change color as they get dim down.
        public bool COOL_LIKE_INCANDESCENT { get; set; } = true;

        #endregion

        // Pallette of 16 colors
        private Color[] gCurrentPalette;
        private Color[] gTargetPalette;

        private bool changepalette = false;
        private bool blendpalette = false;

        /// <summary>
        /// Start Here then get property leds
        /// call loop often, properties can be adjusted between loops
        /// </summary>
        public async Task loop()
        {
            if (!pstopWatch.IsRunning)
                pstopWatch.Start();
            if (!blendsw.IsRunning)
                blendsw.Start();
            if (!fpsTimer.IsRunning)
                fpsTimer.Start();

            // change palette
            if (pstopWatch.ElapsedMilliseconds / 1000 >= SECONDS_PER_PALETTE)
                changepalette = true;

            if (changepalette)
            {
                pstopWatch.Restart();
                changepalette = false;
                gTargetPalette = ChooseNextColorPalette();
            }

            // blend palette
            if (blendsw.ElapsedMilliseconds >= 10)
                blendpalette = true;

            if (blendpalette)
            {
                blendsw.Restart();
                blendpalette = false;
                gCurrentPalette = NblendPaletteTowardPalette(gCurrentPalette, gTargetPalette, 12);
            }

            await Task.Run(() => DrawTwinkles());

            // Now the leds Frame is ready
            LoopCount++;
            FPS = LoopCount * 1000.0 / fpsTimer.ElapsedMilliseconds;
        }

        public void Reset()
        {
            Leds = new byte[NUM_LEDS * bytesperled];
            LoopCount = 0;

            pal = -1;
            CurrentPalette = string.Empty;
            gTargetPalette = ChooseNextColorPalette();
            gCurrentPalette = (Color[])gTargetPalette.Clone();

            pstopWatch.Stop();
            pstopWatch.Reset();
            blendsw.Stop();
            blendsw.Reset();
            fpsTimer.Stop();
            fpsTimer.Reset();
        }

        #region Private Methods

        /// <summary>
        /// FastLed function implemented based on desc - this is called often (10ms)
        /// Alter one palette by making it slightly more like a "target palette".
        ///  Used for palette cross-fades.
        ///  It does this by comparing each of the R, G, and B channels of each entry in the current
        ///  palette to the corresponding entry in the target palette and making small adjustments:
        ///          If the CRGB::red channel is too low, it will be increased.
        ///          If the CRGB::red channel is too high, it will be slightly reduced.
        ///  ...and so on and so forth for the CRGB::green and CRGB::blue channels.
        ///
        /// Additionally, there are two significant visual improvements to this algorithm implemented here.First is this:
        ///          When increasing a channel, it is stepped up by ONE.
        ///          When decreasing a channel, it is stepped down by TWO.
        /// Due to the way the eye perceives light, and the way colors are represented in RGB, this
        /// produces a more uniform apparent brightness when cross-fading between most palette colors.
        ///
        ///        The second visual tweak is limiting the number of changes that will be made to the
        ///        palette at once. If all the palette entries are changed at once, it can give a muddled appearance.
        ///        However, if only a few palette entries are changed at once, you get a visually smoother transition:
        ///        in the middle of the cross-fade your current palette will actually contain some colors from the old
        ///        palette, a few blended colors,  and some colors from the new palette.
        ///        The limit is 48 (16 color entries times 3 channels each). The default is 24, meaning that only half of the
        ///        palette entries can be changed per call.
        /// </summary>
        /// <param name="curr">currentPalette  the palette to modify.</param>
        /// <param name="targ">targetPalette the palette to move towards</param>
        /// <param name="v">maxChanges the maximum number of possible palette changes to make to the color channels per call.</param>
        private static Color[] NblendPaletteTowardPalette(Color[] curr, Color[] targ, byte v)
        {
            if (curr.SequenceEqual(targ))
                return curr;

            var outpal = (Color[])curr.Clone();
            byte r, g, b;
            byte changes = 0;
            for (var i = 0; i < targ.Length; i++)
            {
                var c1 = outpal[i];
                var c2 = targ[i];

                if (c1 != c2 && changes <= (byte)Math.Min(v, (byte)48))
                {
                    // R
                    r = c1.R;
                    if (c1.R < c2.R)
                    {
                        r = (byte)Math.Min(c1.R + 1, 255);
                        changes++;
                    }
                    else if (c1.R > c2.R)
                    {
                        r = (byte)Math.Max(c1.R - 2, 0);
                        changes++;
                    }

                    // G
                    g = c1.G;
                    if (c1.G < c2.G)
                    {
                        g = (byte)Math.Min(c1.G + 1, 255);
                        changes++;
                    }
                    else if (c1.G > c2.G)
                    {
                        g = (byte)Math.Max(c1.G - 2, 0);
                        changes++;
                    }

                    // B
                    b = c1.B;
                    if (c1.B < c2.B)
                    {
                        b = (byte)Math.Min(c1.B + 1, 255);
                        changes++;
                    }
                    else if (c1.B > c2.B)
                    {
                        b = (byte)Math.Max(c1.B - 2, 0);
                        changes++;
                    }

                    outpal[i] = Color.FromArgb(r, g, b);
                }
            }
            return outpal;
        }

        //  This function loops over each pixel, calculates the 
        //  adjusted 'clock' that this pixel should use, and calls 
        //  "CalculateOneTwinkle" on each pixel.  It then displays
        //  either the twinkle color of the background color, 
        //  whichever is brighter.
        private void DrawTwinkles()
        {
            // "PRNG16" is the pseudorandom number generator
            // It MUST be reset to the same starting value each time
            // this function is called, so that the sequence of 'random'
            // numbers that it generates is (paradoxically) stable.
            ushort PRNG16 = 11337;

            uint clock32 = (uint)pstopWatch.ElapsedMilliseconds;

            // Set up the background color, "bg".
            // if AUTO_SELECT_BACKGROUND_COLOR == 1, and the first two colors of
            // the current palette are identical, then a deeply faded version of
            // that color is used for the background color
            Color bg;
            if (AUTO_SELECT_BACKGROUND_COLOR &&
                (gCurrentPalette[0] == gCurrentPalette[1]))
            {
                bg = gCurrentPalette[0];
                byte bglight = (byte)HSBColor.FromColor(bg).B;
                if (bglight > 64)
                {
                    bg = bg.nscale8_video(16); // very bright, so scale to 1/16th
                }
                else if (bglight > 16)
                {
                    bg = bg.nscale8_video(64); // not that bright, so scale to 1/4th
                }
                else
                {
                    bg = bg.nscale8_video(86); // dim, scale to 1/3rd.
                }
            }
            else
            {
                bg = gBackgroundColor; // just use the explicitly defined background color
            }

            byte backgroundBrightness = (byte)HSBColor.FromColor(bg).B;
            Color pixel;

            for (var i = 0; i < Leds.Length; i = i + bytesperled)
            {               
                PRNG16 = (ushort)((PRNG16 * 2053) + 1384); // next 'random' number
                ushort myclockoffset16 = PRNG16; // use that number as clock offset
                PRNG16 = (ushort)((PRNG16 * 2053) + 1384); // next 'random' number
                                                           // use that number as clock speed adjustment factor (in 8ths, from 8/8ths to 23/8ths)
                byte myspeedmultiplierQ5_3 = (byte)(((((PRNG16 & 0xFF) >> 4) + (PRNG16 & 0x0F)) & 0x0F) + 0x08);
                uint myclock30 = (uint)((clock32 * myspeedmultiplierQ5_3) >> 3) + myclockoffset16;
                byte myunique8 = (byte)(PRNG16 >> 8); // get 'salt' value for this pixel

                // We now have the adjusted 'clock' for this pixel, now we call
                // the function that computes what color the pixel should be based
                // on the "brightness = f( time )" idea.
                Color c = ComputeOneTwinkle(myclock30, myunique8);

                byte cbright = (byte)HSBColor.FromColor(c).B;
                short deltabright = (short)(cbright - backgroundBrightness);
                if (deltabright >= 32) // || !bg ??
                {
                    // If the new pixel is significantly brighter than the background color, 
                    // use the new color.
                    pixel = c;
                }
                else if (deltabright > 0)
                {
                    // If the new pixel is just slightly brighter than the background color,
                    // mix a blend of the new color and the background color
                    pixel = Blend(bg, c, deltabright * 8);
                }
                else
                {
                    // if the new pixel is not at all brighter than the background color,
                    // just use the background color.
                    pixel = bg;
                }

                var j = i;
                if (bytesperled == 4)
                {
                    // skip white for WRGB
                    Leds[j] = 0;
                    j++;
                   
                }
                Leds[j] = pixel.R;
                Leds[j + 1] = pixel.G;
                Leds[j + 2] = pixel.B;
            }
        }

        /// <summary>Blends the specified colors together.</summary>
        /// <param name="c1">Color to blend onto the background color.</param>
        /// <param name="c2">Color to blend the other color onto.</param>
        /// <param name="t">How much of <paramref name="c1"/> to keep,
        /// “on top of” <paramref name="c2"/>.</param>
        /// <returns>The blended colors.</returns>
        private static Color Blend(Color c1, Color c2, double t)
        {
            // R
            byte r = c2.R;
            if (c1.R > c2.R)
                r = (byte)(r + (c1.R - c2.R) * t / 255);
            else
                r = (byte)(r + (c2.R - c1.R) * t / 255);

            // G
            byte g = c2.G;
            if (c1.G > c2.G)
                g = (byte)(g - (c1.G - c2.G) * t / 255);
            else
                g = (byte)(g + (c2.G - c1.G) * t / 255);

            // B
            byte b = c2.B;
            if (c1.B > c2.B)
                b = (byte)(b - (c1.B - c2.B) * t / 255);
            else
                b = (byte)(b + (c2.B - c1.B) * t / 255);

            return Color.FromArgb(r, g, b);
        }

        //  This function takes a time in pseudo-milliseconds,
        //  figures out brightness = f( time ), and also hue = f( time )
        //  The 'low digits' of the millisecond time are used as 
        //  input to the brightness wave function.  
        //  The 'high digits' are used to select a color, so that the color
        //  does not change over the course of the fade-in, fade-out
        //  of one cycle of the brightness wave function.
        //  The 'high digits' are also used to determine whether this pixel
        //  should light at all during this cycle, based on the TWINKLE_DENSITY.
        private Color ComputeOneTwinkle(uint ms, byte salt)
        {
            ushort ticks = (ushort)(ms >> (8 - TWINKLE_SPEED));
            byte fastcycle8 = (byte)ticks;
            ushort slowcycle16 = (ushort)((ticks >> 8) + salt);
            slowcycle16 += (ushort)(Math.Sin(slowcycle16 * 360.0 / 255) * 255);
            slowcycle16 = (ushort)((slowcycle16 * 2053) + 1384);
            byte slowcycle8 = (byte)((slowcycle16 & 0xFF) + (slowcycle16 >> 8));

            byte bright = 0;
            if (((slowcycle8 & 0x0E) / 2) < TWINKLE_DENSITY)
            {
                bright = AttackDecayWave8(fastcycle8);
            }

            byte hue = (byte)(slowcycle8 - salt);
            Color c;
            if (bright > 0)
            {
                c = ColorFromPalette(gCurrentPalette, hue, bright);
                if (COOL_LIKE_INCANDESCENT)
                {
                    CoolLikeIncandescent(c, fastcycle8);
                }
            }
            else
            {
                c = Color.Black;
            }
            return c;
        }

        /// <summary>
        /// get the palette item by index, then brighten it
        /// Regardless of the number of entries in the base palette, this function will interpolate 
        /// between entries to turn the discrete colors into a smooth gradient.
        /// </summary>
        /// <param name="p">pal the palette to retrieve the color from</param>
        /// <param name="hue">index into 16 expanded to 256 palette</param>
        /// <param name="bright">brightness optional brightness value to scale the resulting color</param>
        /// <returns>Color</returns>
        private static Color ColorFromPalette(Color[] p, byte hue, byte bright)
        {
            // we have 16 but we virtually have 256
            var start = hue / 16;
            var offset = hue % 16;

            Color entry;
            if (offset == 0)
                entry = p[start];
            else
                entry = Interpolate(p, start, offset);

            var result = HSBColor.FromColor(entry);
            return HSBColor.FromHSB(new HSBColor(result.H, result.S, bright));
        }

        private static Color Interpolate(Color[] p, int start, int offset)
        {
            var c1 = p[start];
            var c2 = c1;
            if (start == p.Length - 1)
                c2 = p[0];
            else
                c2 = p[start + 1];

            var t = offset / 16.0;
            byte r = c1.R;
            byte g = c1.G;
            byte b = c1.B;

            if (t > 0)
            {
                // R
                if (c1.R > c2.R)
                    r = (byte)(c1.R - (c1.R - c2.R) * t);
                else
                    r = (byte)(c1.R + (c2.R - c1.R) * t);

                // G
                if (c1.G > c2.G)
                    g = (byte)(c1.G - (c1.G - c2.G) * t);
                else
                    g = (byte)(c1.G + (c2.G - c1.G) * t);

                // B
                if (c1.B > c2.B)
                    b = (byte)(c1.B - (c1.B - c2.B) * t);
                else
                    b = (byte)(c1.B + (c2.B - c1.B) * t);
            }

            return Color.FromArgb(r, g, b);
        }

        // This function is like 'triwave8', which produces a 
        // symmetrical up-and-down triangle sawtooth waveform, except that this
        // function produces a triangle wave with a faster attack and a slower decay:
        //
        //     / \ 
        //    /     \ 
        //   /         \ 
        //  /             \ 
        //

        private static byte AttackDecayWave8(byte i)
        {
            if (i < 86)
            {
                return (byte)(i * 3);
            }
            else
            {
                i -= 86;
                return (byte)(255 - (i + (i / 2)));
            }
        }

        // This function takes a pixel, and if its in the 'fading down'
        // part of the cycle, it adjusts the color a little bit like the 
        // way that incandescent bulbs fade toward 'red' as they dim.
        private static Color CoolLikeIncandescent(Color c, byte phase)
        {
            if (phase < 128) return c;

            byte cooling = (byte)((phase - 128) >> 4);

            var G = c.G;
            if (cooling < G)
                G -= cooling;
            else
                G = 0;

            var B = c.B;
            if ((cooling * 2) < B)
                B -= (byte)(cooling * 2);
            else
                B = 0;

            return Color.FromArgb(c.R, G, B);
        }

        #endregion

        #region Palettes

        // A mostly red palette with green accents and white trim.
        // "CRGB::Gray" is used as white to keep the brightness more uniform.
        private static readonly Color[] RedGreenWhite_p = new Color[]
        {
            Color.Red,Color.Red,Color.Red,Color.Red,
            Color.Red,Color.Red,Color.Red,Color.Red,
            Color.Red,Color.Red,Color.Gray,Color.Gray,
            Color.Green,Color.Green,Color.Green,Color.Green
        };

        // A mostly (dark) green palette with red berries.0c);
        private static readonly Color Holly_Green = Color.FromArgb(0x00580c);
        private static readonly Color Holly_Red = Color.FromArgb(0xB00402);
        private static readonly Color[] Holly_p = new Color[]
        {
            Holly_Green, Holly_Green, Holly_Green, Holly_Green,
            Holly_Green, Holly_Green, Holly_Green, Holly_Green,
            Holly_Green, Holly_Green, Holly_Green, Holly_Green,
            Holly_Green, Holly_Green, Holly_Green, Holly_Red
        };

        // A red and white striped palette
        // "CRGB::Gray" is used as white to keep the brightness more uniform.
        private static readonly Color[] RedWhite_p = new Color[]
        {
            Color.Red,Color.Red,Color.Red,Color.Red,
            Color.Gray,Color.Gray,Color.Gray,Color.Gray,
            Color.Red,Color.Red,Color.Red,Color.Red,
            Color.Gray,Color.Gray,Color.Gray,Color.Gray
        };

        // A mostly blue palette with white accents.
        // "CRGB::Gray" is used as white to keep the brightness more uniform.
        private static readonly Color[] BlueWhite_p = new Color[]
        {
            Color.Blue,Color.Blue,Color.Blue,Color.Blue,
            Color.Blue,Color.Blue,Color.Blue,Color.Blue,
            Color.Blue,Color.Blue,Color.Blue,Color.Blue,
            Color.Blue,Color.Gray,Color.Gray,Color.Gray
        };

        //A pure "fairy light" palette with some brightness variations
        private const int FL = 0xFFE42D;
        private static readonly Color FairyLight = Color.FromArgb(FL);
        private static readonly Color HALFFAIRY = Color.FromArgb((FL & 0xFEFEFE) / 2);
        private static readonly Color QUARTERFAIRY = Color.FromArgb((FL & 0xFCFCFC) / 4);
        private static readonly Color[] FairyLight_p = new Color[]
        {
            FairyLight, FairyLight, FairyLight, FairyLight,
            HALFFAIRY, HALFFAIRY, FairyLight, FairyLight,
            QUARTERFAIRY, QUARTERFAIRY, FairyLight, FairyLight,
            FairyLight, FairyLight, FairyLight, FairyLight
        };

        // A palette of soft snowflakes with the occasional bright one
        private static readonly Color[] Snow_p = new Color[]
        {
            Color.FromArgb(0x304048), Color.FromArgb(0x304048), Color.FromArgb(0x304048), Color.FromArgb(0x304048),
            Color.FromArgb(0x304048), Color.FromArgb(0x304048), Color.FromArgb(0x304048), Color.FromArgb(0x304048),
            Color.FromArgb(0x304048), Color.FromArgb(0x304048), Color.FromArgb(0x304048), Color.FromArgb(0x304048),
            Color.FromArgb(0x304048), Color.FromArgb(0x304048), Color.FromArgb(0x304048), Color.FromArgb(0xE0F0FF)
        };

        // A palette reminiscent of large 'old-school' C9-size tree lights
        // in the five classic colors: red, orange, green, blue, and white.
        private static readonly Color C9_Red = Color.FromArgb(0xB8, 0x04, 0x00);
        private static readonly Color C9_Orange = Color.FromArgb(0x90, 0x2C, 0x02);
        private static readonly Color C9_Green = Color.FromArgb(0x04, 0x60, 0x02);
        private static readonly Color C9_Blue = Color.FromArgb(0x07, 0x07, 0x58);
        private static readonly Color C9_White = Color.FromArgb(0x6, 0x68, 0x20);
        private static readonly Color[] RetroC9_p = new Color[]
        {
            C9_Red, C9_Orange, C9_Red, C9_Orange,
            C9_Orange, C9_Red, C9_Orange, C9_Red,
            C9_Green, C9_Green, C9_Green, C9_Green,
            C9_Blue, C9_Blue, C9_Blue, C9_White
        };

        // A cold, icy pale blue palette
        private static readonly Color Ice_Blue1 = Color.FromArgb(0x0C, 0x10, 0x40);
        private static readonly Color Ice_Blue2 = Color.FromArgb(0x18, 0x20, 0x80);
        private static readonly Color Ice_Blue3 = Color.FromArgb(0x50, 0x80, 0xC0);
        private static readonly Color[] Ice_p = new Color[]
        {
            Ice_Blue1, Ice_Blue1, Ice_Blue1, Ice_Blue1,
            Ice_Blue1, Ice_Blue1, Ice_Blue1, Ice_Blue1,
            Ice_Blue1, Ice_Blue1, Ice_Blue1, Ice_Blue1,
            Ice_Blue2, Ice_Blue2, Ice_Blue2, Ice_Blue3
        };

        // From FastLED
        private static readonly Color[] RainbowColors_p = new Color[]
        {
            Color.FromArgb(0xFF0000), Color.FromArgb(0xD52A00), Color.FromArgb(0xAB5500), Color.FromArgb(0xAB7F00),
            Color.FromArgb(0xABAB00), Color.FromArgb(0x56D500), Color.FromArgb(0x00FF00), Color.FromArgb(0x00D52A),
            Color.FromArgb(0x00AB55), Color.FromArgb(0x0056AA), Color.FromArgb(0x0000FF), Color.FromArgb(0x2A00D5),
            Color.FromArgb(0x5500AB), Color.FromArgb(0x7F0081), Color.FromArgb(0xAB0055), Color.FromArgb(0xD5002B)
        };

        // From FastLED
        private static readonly Color[] PartyColors_p = new Color[]
        {
            Color.FromArgb(0x5500AB), Color.FromArgb(0x84007C), Color.FromArgb(0xB5004B), Color.FromArgb(0xE5001B),
            Color.FromArgb(0xE81700), Color.FromArgb(0xB84700), Color.FromArgb(0xAB7700), Color.FromArgb(0xABAB00),
            Color.FromArgb(0xAB5500), Color.FromArgb(0xDD2200), Color.FromArgb(0xF2000E), Color.FromArgb(0xC2003E),
            Color.FromArgb(0x8F0071), Color.FromArgb(0x5F00A1), Color.FromArgb(0x2F00D0), Color.FromArgb(0x0007F9)
        };

        // Add or remove palette names from this list to control which color
        // palettes are used, and in what order.
        private static List<Color[]> ActivePaletteList = new List<Color[]>()
        {
            RetroC9_p,
            BlueWhite_p,
            RainbowColors_p,
            FairyLight_p,
            RedGreenWhite_p,
            PartyColors_p,
            RedWhite_p,
            Snow_p,
            Holly_p,
            Ice_p
        };

        private static List<string> ActivePaletteNamesList = new List<string>()
        {
            "Retro C9",
            "Blue White",
            "Rainbow Colors",
            "Fairy Light",
            "Red Green White",
            "Party Colors",
            "Red White",
            "Snow",
            "Holly",
            "Ice"
        };

        private int pal = -1;
        // Advance to the next color palette in the list (above).
        private Color[] ChooseNextColorPalette()
        {
            pal += 1;
            if (pal >= ActivePaletteList.Count)
                pal = 0;

            CurrentPalette = ActivePaletteNamesList[pal];
            return ActivePaletteList[pal];
        }

        #endregion

    }
}