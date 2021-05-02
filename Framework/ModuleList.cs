using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace Foster.Framework
{
    /// <summary>
    /// A List of Modules
    /// </summary>
    public class ModuleList : IEnumerable<Module>
    {

        private readonly List<Type> _registered = new List<Type>();
        private readonly List<Module?> _modules = new List<Module?>();
        private readonly Dictionary<Type, Module> _modulesByType = new Dictionary<Type, Module>();
        private bool _immediateInit;
        private bool _immediateStart;

        /// <summary>
        /// Registers a Module
        /// </summary>
        public void Register<T>() where T : Module
        {
            Register(typeof(T));
        }

        /// <summary>
        /// Registers a Module
        /// </summary>
        public void Register(Type type)
        {
            if (_immediateInit)
            {
                var module = Instantiate(type);

                if (_immediateStart)
                {
                    StartupModule(module, true);
                }
            }
            else
            {
                _registered.Add(type);
            }
        }

        /// <summary>
        /// Registers a Module
        /// </summary>
        private Module Instantiate(Type type)
        {
            if (!(Activator.CreateInstance(type) is Module module))
            {
                throw new Exception("Type must inheirt from Module");
            }

            // add Module to lookup
            while (type != typeof(Module) && type != typeof(AppModule))
            {
                if (!_modulesByType.ContainsKey(type))
                {
                    _modulesByType[type] = module;
                }

                if (type.BaseType == null)
                {
                    break;
                }

                type = type.BaseType;
            }

            // insert in order
            var insert = 0;
            while (insert < _modules.Count && (_modules[insert]?.Priority ?? int.MinValue) <= module.Priority)
            {
                insert++;
            }

            _modules.Insert(insert, module);

            // registered
            module.IsRegistered = true;
            module.MainThreadId = Thread.CurrentThread.ManagedThreadId;
            return module;
        }

        /// <summary>
        /// Removes a Module
        /// Note: Removing core modules (such as System) will make everything break
        /// </summary>
        public void Remove(Module module)
        {
            if (!module.IsRegistered)
            {
                throw new Exception("Module is not already registered");
            }

            module.Shutdown();
            module.Disposed();

            var index = _modules.IndexOf(module);
            _modules[index] = null;

            var type = module.GetType();
            while (type != typeof(Module))
            {
                if (_modulesByType[type] == module)
                {
                    _modulesByType.Remove(type);
                }

                if (type.BaseType == null)
                {
                    break;
                }

                type = type.BaseType;
            }

            module.IsRegistered = false;
        }

        /// <summary>
        /// Tries to get the First Module of the given type
        /// </summary>
        public bool TryGet<T>(out T? module) where T : Module
        {
            if (_modulesByType.TryGetValue(typeof(T), out var m))
            {
                module = (T)m;
                return true;
            }

            module = null;
            return false;
        }

        /// <summary>
        /// Tries to get the First Module of the given type
        /// </summary>
        public bool TryGet(Type type, out Module? module)
        {
            if (_modulesByType.TryGetValue(type, out var m))
            {
                module = m;
                return true;
            }

            module = null;
            return false;
        }

        /// <summary>
        /// Gets the First Module of the given type, if it exists, or throws an Exception
        /// </summary>
        public T Get<T>() where T : Module
        {
            if (!_modulesByType.TryGetValue(typeof(T), out var module))
            {
                throw new Exception($"App is does not have a {typeof(T).Name} Module registered");
            }

            return (T)module;
        }

        /// <summary>
        /// Gets the First Module of the given type, if it exists, or throws an Exception
        /// </summary>
        public Module Get(Type type)
        {
            if (!_modulesByType.TryGetValue(type, out var module))
            {
                throw new Exception($"App is does not have a {type.Name} Module registered");
            }

            return module;
        }

        /// <summary>
        /// Checks if a Module of the given type exists
        /// </summary>
        public bool Has<T>() where T : Module
        {
            return _modulesByType.ContainsKey(typeof(T));
        }

        /// <summary>
        /// Checks if a Module of the given type exists
        /// </summary>
        public bool Has(Type type)
        {
            return _modulesByType.ContainsKey(type);
        }

        internal void ApplicationStarted()
        {
            // create Application Modules
            for (int i = 0; i < _registered.Count; i++)
            {
                if (typeof(AppModule).IsAssignableFrom(_registered[i]))
                {
                    Instantiate(_registered[i]);
                    _registered.RemoveAt(i);
                    i--;
                }
            }

            for (int i = 0; i < _modules.Count; i++)
            {
                if (_modules[i] is AppModule module)
                {
                    module.ApplicationStarted();
                }
            }
        }

        internal void FirstWindowCreated()
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                if (_modules[i] is AppModule module)
                {
                    module.FirstWindowCreated();
                }
            }
        }

        internal void Startup()
        {
            // this method is a little strange because it makes sure all App Modules have
            // had their Startup methods called BEFORE instantiating normal Modules
            // Thus it has to iterate over modules and call Startup twice

            // run startup on on App Modules
            for (int i = 0; i < _modules.Count; i++)
            {
                StartupModule(_modules[i], false);
            }

            // instantiate remaining modules that are registered
            for (int i = 0; i < _registered.Count; i++)
            {
                Instantiate(_registered[i]);
            }

            // further modules will be instantiated immediately
            _immediateInit = true;

            // call started on all modules
            for (int i = 0; i < _modules.Count; i++)
            {
                StartupModule(_modules[i], true);
            }

            // further modules will have Startup called immediately
            _immediateStart = true;
        }

        private static void StartupModule(Module? module, bool callAppMethods)
        {
            if (module != null && !module.IsStarted)
            {
                module.IsStarted = true;

                if (module is AppModule appModule && callAppMethods)
                {
                    appModule.ApplicationStarted();
                    appModule.FirstWindowCreated();
                }

                module.Startup();
            }
        }

        internal void Shutdown()
        {
            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                _modules[i]?.Shutdown();
            }

            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                _modules[i]?.Disposed();
            }

            _registered.Clear();
            _modules.Clear();
            _modulesByType.Clear();
        }

        internal void FrameStart()
        {
            // remove null module entries
            int toRemove;
            while ((toRemove = _modules.IndexOf(null)) >= 0)
            {
                _modules.RemoveAt(toRemove);
            }

            for (int i = 0; i < _modules.Count; i++)
            {
                _modules[i]?.FrameStart();
            }
        }

        internal void FixedUpdate()
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                _modules[i]?.FixedUpdate();
            }
        }

        internal void Update()
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                _modules[i]?.Update();
            }
        }

        internal void FrameEnd()
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                _modules[i]?.FrameEnd();
            }
        }

        internal void BeforeRender()
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                _modules[i]?.BeforeRender();
            }
        }

        internal void AfterRender()
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                _modules[i]?.AfterRender();
            }
        }

        internal void BeforeRenderWindow(Window window)
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                _modules[i]?.BeforeRenderWindow(window);
            }
        }

        internal void AfterRenderWindow(Window window)
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                _modules[i]?.AfterRenderWindow(window);
            }
        }

        public IEnumerator<Module> GetEnumerator()
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                var module = _modules[i];
                if (module != null)
                {
                    yield return module;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                var module = _modules[i];
                if (module != null)
                {
                    yield return module;
                }
            }
        }
    }
}
