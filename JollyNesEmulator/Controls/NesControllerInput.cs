using System.Windows.Input;
using JollyNesEmulator.Emulator;

namespace JollyNesEmulator.Controls
{
    public sealed class NesControllerInput
    {
        private readonly NesController _controller;

        public NesControllerInput(NesController controller)
        {
            _controller = controller;
        }

        public void KeyDown(Key key)
        {
            if (TryGetButton(key, out NesController.Buttons button))
                _controller.SetButton(button, true);
        }

        public void KeyUp(Key key)
        {
            if (TryGetButton(key, out NesController.Buttons button))
                _controller.SetButton(button, false);
        }

        private static bool TryGetButton(Key key, out NesController.Buttons button)
        {
            button = key switch
            {
                Key.Z => NesController.Buttons.A,
                Key.X => NesController.Buttons.B,
                Key.RightShift => NesController.Buttons.Select,
                Key.Enter => NesController.Buttons.Start,
                Key.Up => NesController.Buttons.Up,
                Key.Down => NesController.Buttons.Down,
                Key.Left => NesController.Buttons.Left,
                Key.Right => NesController.Buttons.Right,
                _ => NesController.Buttons.None
            };

            return button != NesController.Buttons.None;
        }
    }
}


