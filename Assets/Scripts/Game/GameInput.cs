using RetroRally.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RetroRally.Game
{
    /// <summary>
    /// Keyboard and gamepad controls from the Input System. Player 1 uses W/S and the first
    /// gamepad, player 2 the arrow keys and the second gamepad; against the CPU player 1 may use
    /// either set of keys and any gamepad.
    /// </summary>
    public sealed class GameInput
    {
        const float StickDeadZone = 0.18f;
        const float StickStep = 0.6f;

        int stickVertical;
        int stickHorizontal;

        static Keyboard Keys => Keyboard.current;

        /// <summary>True after a gamepad was used last (the HUD shows pad prompts then).</summary>
        public bool UsingGamepad { get; private set; }

        static Gamepad PadFor(Side side, bool singlePlayer)
        {
            if (singlePlayer)
            {
                return side == Side.Left ? Gamepad.current : null;
            }
            int index = side == Side.Left ? 0 : 1;
            return Gamepad.all.Count > index ? Gamepad.all[index] : null;
        }

        /// <summary>The paddle input for <paramref name="side"/>, from -1 (down) to +1 (up).</summary>
        public float Paddle(Side side, bool singlePlayer)
        {
            float value = 0f;
            if (Keys != null)
            {
                bool p1Keys = side == Side.Left;
                bool arrows = side == Side.Right || singlePlayer;
                if (p1Keys)
                {
                    value += (Keys.wKey.isPressed ? 1f : 0f) - (Keys.sKey.isPressed ? 1f : 0f);
                }
                if (arrows)
                {
                    value += (Keys.upArrowKey.isPressed ? 1f : 0f) - (Keys.downArrowKey.isPressed ? 1f : 0f);
                }
                if (value != 0f)
                {
                    UsingGamepad = false;
                }
            }
            Gamepad pad = PadFor(side, singlePlayer);
            if (pad != null)
            {
                float stick = pad.leftStick.ReadValue().y;
                float dpad = (pad.dpad.up.isPressed ? 1f : 0f) - (pad.dpad.down.isPressed ? 1f : 0f);
                if (Mathf.Abs(stick) > StickDeadZone || dpad != 0f)
                {
                    value += Mathf.Abs(stick) > StickDeadZone ? stick : 0f;
                    value += dpad;
                    UsingGamepad = true;
                }
            }
            return Mathf.Clamp(value, -1f, 1f);
        }

        /// <summary>Was the serve button for <paramref name="side"/> pressed this frame?</summary>
        public bool Serve(Side side, bool singlePlayer)
        {
            bool pressed = false;
            if (Keys != null)
            {
                if (side == Side.Left)
                {
                    pressed |= Keys.spaceKey.wasPressedThisFrame;
                    if (singlePlayer)
                    {
                        pressed |= Keys.enterKey.wasPressedThisFrame || Keys.numpadEnterKey.wasPressedThisFrame;
                    }
                }
                else
                {
                    pressed |= Keys.enterKey.wasPressedThisFrame || Keys.numpadEnterKey.wasPressedThisFrame;
                }
            }
            Gamepad pad = PadFor(side, singlePlayer);
            if (pad != null && pad.buttonSouth.wasPressedThisFrame)
            {
                UsingGamepad = true;
                pressed = true;
            }
            return pressed;
        }

        public bool Pause => (Keys != null && (Keys.escapeKey.wasPressedThisFrame || Keys.pKey.wasPressedThisFrame)) ||
            AnyPad(pad => pad.startButton.wasPressedThisFrame);

        public bool Mute => Keys != null && Keys.mKey.wasPressedThisFrame;

        public bool Confirm =>
            (Keys != null && (Keys.enterKey.wasPressedThisFrame || Keys.numpadEnterKey.wasPressedThisFrame || Keys.spaceKey.wasPressedThisFrame)) ||
            AnyPad(pad => pad.buttonSouth.wasPressedThisFrame);

        public bool Back =>
            (Keys != null && (Keys.escapeKey.wasPressedThisFrame || Keys.backspaceKey.wasPressedThisFrame)) ||
            AnyPad(pad => pad.buttonEast.wasPressedThisFrame);

        /// <summary>-1 for a step up a menu, +1 for a step down, 0 otherwise.</summary>
        public int VerticalStep()
        {
            int step = 0;
            if (Keys != null)
            {
                if (Keys.upArrowKey.wasPressedThisFrame || Keys.wKey.wasPressedThisFrame) step = -1;
                if (Keys.downArrowKey.wasPressedThisFrame || Keys.sKey.wasPressedThisFrame) step = 1;
            }
            return step != 0 ? step : PadStep(true, ref stickVertical);
        }

        /// <summary>-1 for a step left through a setting, +1 for a step right, 0 otherwise.</summary>
        public int HorizontalStep()
        {
            int step = 0;
            if (Keys != null)
            {
                if (Keys.leftArrowKey.wasPressedThisFrame || Keys.aKey.wasPressedThisFrame) step = -1;
                if (Keys.rightArrowKey.wasPressedThisFrame || Keys.dKey.wasPressedThisFrame) step = 1;
            }
            return step != 0 ? step : PadStep(false, ref stickHorizontal);
        }

        int PadStep(bool vertical, ref int held)
        {
            int step = 0;
            int stickSign = 0;
            foreach (Gamepad pad in Gamepad.all)
            {
                if (vertical)
                {
                    if (pad.dpad.up.wasPressedThisFrame) step = -1;
                    if (pad.dpad.down.wasPressedThisFrame) step = 1;
                }
                else
                {
                    if (pad.dpad.left.wasPressedThisFrame) step = -1;
                    if (pad.dpad.right.wasPressedThisFrame) step = 1;
                }
                Vector2 stick = pad.leftStick.ReadValue();
                float axis = vertical ? -stick.y : stick.x;
                if (Mathf.Abs(axis) > StickStep)
                {
                    stickSign = axis > 0f ? 1 : -1;
                }
            }
            if (step == 0 && stickSign != 0 && stickSign != held)
            {
                step = stickSign;
            }
            held = stickSign;
            if (step != 0)
            {
                UsingGamepad = true;
            }
            return step;
        }

        static bool AnyPad(System.Func<Gamepad, bool> test)
        {
            foreach (Gamepad pad in Gamepad.all)
            {
                if (test(pad))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
