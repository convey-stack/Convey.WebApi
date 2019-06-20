using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace Convey.WebApi.Helpers
{
    public static class Extensions
    {
        public static object SetDefaultInstanceProperties(this object instance)
        {
            var type = instance.GetType();
            foreach (var propertyInfo in type.GetProperties())
            {
                if (propertyInfo.PropertyType == typeof(string))
                {
                    SetDefaultValue(propertyInfo, instance, string.Empty);
                    continue;
                }

                if (!propertyInfo.PropertyType.IsClass)
                {
                    continue;
                }

                var propertyInstance = FormatterServices.GetUninitializedObject(propertyInfo.PropertyType);
                SetDefaultValue(propertyInfo, instance, propertyInstance);
                SetDefaultInstanceProperties(propertyInstance);
            }

            return instance;
        }

        private static void SetDefaultValue(PropertyInfo propertyInfo, object instance, object value)
        {
            if (propertyInfo.CanWrite)
            {
                propertyInfo.SetValue(instance, value);
                return;
            }

            var propertyName = propertyInfo.Name;
            var field = instance.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .SingleOrDefault(x => x.Name.StartsWith($"<{propertyName}>"));
            field?.SetValue(instance, value);
        }
    }
}