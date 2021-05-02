using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;

namespace Foster.Framework
{
    /// <summary>
    /// The Input Manager that stores the current Input State
    /// </summary>
    public abstract class Input
    {
        /// <summary>
        /// The Current Input State
        /// </summary>
        public InputState State { get; }

        /// <summary>
        /// The Input State of the previous frame
        /// </summary>
        public InputState LastState { get; }

        /// <summary>
        /// The Input State of the next frame
        /// </summary>
        private readonly InputState _nextState;

        /// <summary>
        /// The Keyboard of the current State
        /// </summary>
        public Keyboard Keyboard => State.Keyboard;

        /// <summary>
        /// The Mouse of the Current State
        /// </summary>
        public Mouse Mouse => State.Mouse;

        /// <summary>
        /// The Controllers of the Current State
        /// </summary>
        public ReadOnlyCollection<Controller> Controllers => State.Controllers;

        /// <summary>
        /// Default delay before a key or button starts repeating
        /// </summary>
        public float RepeatDelay { get; set; } = 0.4f;

        /// <summary>
        /// Default interval that the repeat is triggered, in seconds
        /// </summary>
        public float RepeatInterval { get; set; } = 0.03f;

        internal List<WeakReference<VirtualButton>> VirtualButtons { get; } = new List<WeakReference<VirtualButton>>();

        protected Input()
        {
            State = new InputState(this);
            LastState = new InputState(this);
            _nextState = new InputState(this);
        }

        internal void Step()
        {
            LastState.Copy(State);
            State.Copy(_nextState);
            _nextState.Step();

            for (int i = VirtualButtons.Count - 1; i >= 0; i--)
            {
                var button = VirtualButtons[i];
                if (button.TryGetTarget(out var target))
                {
                    target.Update();
                }
                else
                {
                    VirtualButtons.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Sets the Mouse Cursor
        /// </summary>
        public abstract void SetMouseCursor(Cursors cursors);

        /// <summary>
        /// Gets the Clipboard String, if it is a String
        /// </summary>
        public abstract string? GetClipboardString();

        /// <summary>
        /// Sets the Clipboard to the given String
        /// </summary>
        public abstract void SetClipboardString(string value);

        public delegate void TextInputHandler(char value);

        public event TextInputHandler? OnTextEvent;

        protected void OnText(char value)
        {
            OnTextEvent?.Invoke(value);
            _nextState.Keyboard.Text.Append(value);
        }

        protected void OnKeyDown(Keys key)
        {
            uint id = (uint)key;
            if (id >= Keyboard.MaxKeys)
            {
                throw new ArgumentOutOfRangeException(nameof(key), "Value is out of Range for supported keys");
            }

            _nextState.Keyboard.down[id] = true;
            _nextState.Keyboard.pressed[id] = true;
            _nextState.Keyboard.timestamp[id] = Time.Duration.Ticks;
        }

        protected void OnKeyUp(Keys key)
        {
            uint id = (uint)key;
            if (id >= Keyboard.MaxKeys)
            {
                throw new ArgumentOutOfRangeException(nameof(key), "Value is out of Range for supported keys");
            }

            _nextState.Keyboard.down[id] = false;
            _nextState.Keyboard.released[id] = true;
        }

        protected void OnMouseDown(MouseButtons button)
        {
            _nextState.Mouse.down[(int)button] = true;
            _nextState.Mouse.pressed[(int)button] = true;
            _nextState.Mouse.timestamp[(int)button] = Time.Duration.Ticks;
        }

        protected void OnMouseUp(MouseButtons button)
        {
            _nextState.Mouse.down[(int)button] = false;
            _nextState.Mouse.released[(int)button] = true;
        }

        protected void OnMouseWheel(float offsetX, float offsetY)
        {
            _nextState.Mouse.wheelValue = new Vector2(offsetX, offsetY);
        }

        protected void OnJoystickConnect(uint index, string name, uint buttonCount, uint axisCount, bool isGamepad)
        {
            if (index < InputState.MaxControllers)
            {
                _nextState.Controllers[(int)index].Connect(name, buttonCount, axisCount, isGamepad);
            }
        }

        protected void OnJoystickDisconnect(uint index)
        {
            if (index < InputState.MaxControllers)
            {
                _nextState.Controllers[(int)index].Disconnect();
            }
        }

        protected void OnJoystickButtonDown(uint index, uint button)
        {
            if (index < InputState.MaxControllers && button < Controller.MaxButtons)
            {
                _nextState.Controllers[(int)index].down[button] = true;
                _nextState.Controllers[(int)index].pressed[button] = true;
                _nextState.Controllers[(int)index].timestamp[button] = Time.Duration.Ticks;
            }
        }

        protected void OnJoystickButtonUp(uint index, uint button)
        {
            if (index < InputState.MaxControllers && button < Controller.MaxButtons)
            {
                _nextState.Controllers[(int)index].down[button] = false;
                _nextState.Controllers[(int)index].released[button] = true;
            }
        }

        protected void OnGamepadButtonDown(uint index, Buttons button)
        {
            if (index < InputState.MaxControllers && button != Buttons.None)
            {
                _nextState.Controllers[(int)index].down[(int)button] = true;
                _nextState.Controllers[(int)index].pressed[(int)button] = true;
                _nextState.Controllers[(int)index].timestamp[(int)button] = Time.Duration.Ticks;
            }
        }

        protected void OnGamepadButtonUp(uint index, Buttons button)
        {
            if (index < InputState.MaxControllers && button != Buttons.None)
            {
                _nextState.Controllers[(int)index].down[(int)button] = false;
                _nextState.Controllers[(int)index].released[(int)button] = true;
            }
        }

        protected bool IsJoystickButtonDown(uint index, uint button)
        {
            return (index < InputState.MaxControllers && button < Controller.MaxButtons && _nextState.Controllers[(int)index].down[button]);
        }

        protected bool IsGamepadButtonDown(uint index, Buttons button)
        {
            return (index < InputState.MaxControllers && button != Buttons.None && _nextState.Controllers[(int)index].down[(int)button]);
        }

        protected void OnJoystickAxis(uint index, uint axis, float value)
        {
            if (index < InputState.MaxControllers && axis < Controller.MaxAxis)
            {
                _nextState.Controllers[(int)index].axis[axis] = value;
                _nextState.Controllers[(int)index].axisTimestamp[axis] = Time.Duration.Ticks;
            }
        }

        protected float GetJoystickAxis(uint index, uint axis)
        {
            if (index < InputState.MaxControllers && axis < Controller.MaxAxis)
            {
                return _nextState.Controllers[(int)index].axis[axis];
            }

            return 0;
        }

        protected void OnGamepadAxis(uint index, Axes axis, float value)
        {
            if (index < InputState.MaxControllers && axis != Axes.None)
            {
                _nextState.Controllers[(int)index].axis[(int)axis] = value;
                _nextState.Controllers[(int)index].axisTimestamp[(int)axis] = Time.Duration.Ticks;
            }
        }

        protected float GetGamepadAxis(uint index, Axes axis)
        {
            if (index < InputState.MaxControllers && axis != Axes.None)
            {
                return _nextState.Controllers[(int)index].axis[(int)axis];
            }

            return 0;
        }

    }
}
