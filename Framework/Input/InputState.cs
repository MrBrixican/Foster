using System.Collections.ObjectModel;

namespace Foster.Framework
{
    /// <summary>
    /// Stores an Input State
    /// </summary>
    public class InputState
    {
        /// <summary>
        /// The Maximum number of Controllers
        /// </summary>
        public const int MaxControllers = 32;

        /// <summary>
        /// Our Input Module
        /// </summary>
        public Input Input { get; set; }

        /// <summary>
        /// The Keyboard State
        /// </summary>
        public Keyboard Keyboard { get; set; }

        /// <summary>
        /// The Mouse State
        /// </summary>
        public Mouse Mouse { get; set; }

        /// <summary>
        /// A list of all the Controllers
        /// </summary>
        private readonly Controller[] _controllers;

        /// <summary>
        /// A Read-Only Collection of the Controllers
        /// Note that they aren't necessarily connected
        /// </summary>
        public ReadOnlyCollection<Controller> Controllers { get; }

        public InputState(Input input)
        {
            Input = input;

            _controllers = new Controller[MaxControllers];
            for (int i = 0; i < _controllers.Length; i++)
            {
                _controllers[i] = new Controller(input);
            }

            Controllers = new ReadOnlyCollection<Controller>(_controllers);
            Keyboard = new Keyboard(input);
            Mouse = new Mouse();
        }

        internal void Step()
        {
            for (int i = 0; i < Controllers.Count; i++)
            {
                if (Controllers[i].Connected)
                {
                    Controllers[i].Step();
                }
            }
            Keyboard.Step();
            Mouse.Step();
        }

        internal void Copy(InputState other)
        {
            for (int i = 0; i < Controllers.Count; i++)
            {
                if (other.Controllers[i].Connected || (Controllers[i].Connected != other.Controllers[i].Connected))
                {
                    Controllers[i].Copy(other.Controllers[i]);
                }
            }

            Keyboard.Copy(other.Keyboard);
            Mouse.Copy(other.Mouse);
        }
    }
}
