namespace JollyNesEmulator.Emulator
{
    public sealed class NesController
    {
        [System.Flags]
        public enum Buttons : byte
        {
            None = 0x00,
            A = 0x01,
            B = 0x02,
            Select = 0x04,
            Start = 0x08,
            Up = 0x10,
            Down = 0x20,
            Left = 0x40,
            Right = 0x80
        }

        private Buttons _buttons;
        private Buttons _latchedButtons;
        private int _shiftIndex;
        private bool _strobe;

        public Buttons CurrentButtons => _buttons;

        public void SetButtons(Buttons buttons)
        {
            _buttons = buttons;

            if (_strobe)
                _latchedButtons = _buttons;
        }

        public void SetButton(Buttons button, bool pressed)
        {
            if (pressed)
                _buttons |= button;
            else
                _buttons &= ~button;

            if (_strobe)
                _latchedButtons = _buttons;
        }

        public byte Read()
        {
            if (_strobe)
                return (byte)((byte)_buttons & 0x01);

            if (_shiftIndex < 8)
            {
                byte buttons = (byte)_latchedButtons;
                byte result = (byte)((buttons >> _shiftIndex) & 0x01);
                _shiftIndex++;
                return result;
            }

            return 1;
        }

        public void Write(byte value)
        {
            bool newStrobe = (value & 0x01) != 0;

            if (newStrobe)
            {
                _strobe = true;
                _shiftIndex = 0;
                _latchedButtons = _buttons;
                return;
            }

            if (_strobe)
            {
                _latchedButtons = _buttons;
                _shiftIndex = 0;
            }

            _strobe = false;
        }

        public void Reset()
        {
            _buttons = Buttons.None;
            _latchedButtons = Buttons.None;
            _shiftIndex = 0;
            _strobe = false;
        }
    }
}
