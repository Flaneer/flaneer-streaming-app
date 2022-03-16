﻿namespace FlaneerMediaLib
{
    public interface IService
    {
    }
    public class ServiceRegistry
    {
        public static Action<IService> ServiceAdded;
        
        internal Dictionary<Type, IService> registry = new();

        private static ServiceRegistry instance = null;

        private ServiceRegistry()
        {
        }

        private static ServiceRegistry Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new ServiceRegistry();
                }
                return instance;
            }
        }

        public static void AddService<T>(T service) where T : IService
        {
            foreach (var ifce in service.GetType().GetInterfaces())
            {
                Instance.registry.Add(ifce, service);
                ServiceAdded?.Invoke(service);
            }
        }

        public static bool TryGetService<T>(out T service) where T : IService
        {
            IService ret;
            if (Instance.registry.TryGetValue(typeof(T), out ret))
            {
                service = (T) ret;
                return true;
            }
            else
            {
                service = default;
                return false;
            }
        }
    }
}
