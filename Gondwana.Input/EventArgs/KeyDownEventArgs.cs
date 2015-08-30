using Gondwana.Input.Keyboard;
using System.Windows.Forms;

namespace Gondwana.Input.EventArgs
{
    public class KeyDownEventArgs : System.EventArgs
    {
        public KeyEventConfiguration KeyConfig;
        public bool IsShift;
        public bool IsAlt;
        public bool IsCtrl;

        protected internal KeyDownEventArgs(KeyEventConfiguration keycfg)
        {
            KeyConfig = keycfg;
            IsShift = Keyboard.Keyboard.GetAsyncKeyState(Keys.ShiftKey);
            IsAlt = Keyboard.Keyboard.GetAsyncKeyState(Keys.Menu);
            IsCtrl = Keyboard.Keyboard.GetAsyncKeyState(Keys.LControlKey) || Keyboard.Keyboard.GetAsyncKeyState(Keys.RControlKey);
        }
    }
}
