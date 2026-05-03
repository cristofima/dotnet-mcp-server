using Microsoft.Extensions.Configuration;

namespace McpServer.Shared.Extensions;

/// <summary>
/// Extension methods for <see cref="IConfiguration"/> providing safe, typed section binding.
/// </summary>
public static class ConfigurationExtensions
{
    extension(IConfiguration configuration)
    {
        /// <summary>
        /// Binds a required configuration section to <typeparamref name="T"/> and returns the result.
        /// Throws <see cref="InvalidOperationException"/> if the section is absent, naming the missing
        /// section in the message. Individual required fields are validated by <c>ValidateOnStart</c>
        /// via <see cref="System.ComponentModel.DataAnnotations.RequiredAttribute"/>.
        /// </summary>
        public T GetRequiredSection<T>(string sectionName) where T : new()
        {
            var section = configuration.GetSection(sectionName);
            if (!section.Exists())
            {
                throw new InvalidOperationException(
                    $"Required {nameof(configuration)} section '{sectionName}' is missing.");
            }

            var instance = new T();
            section.Bind(instance);
            return instance;
        }
    }
}
