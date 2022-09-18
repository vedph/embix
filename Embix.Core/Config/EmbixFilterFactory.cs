using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Embix.Core.Filters;
using Fusi.Text.Unicode;
using Fusi.Tools.Config;
using Microsoft.Extensions.Configuration;
using SimpleInjector;

namespace Embix.Core.Config
{
    /// <summary>
    /// The filters factory. This is used to generate a set of
    /// <see cref="ITextFilter"/>'s from a specified definition of a filters
    /// chain. A filter chain definition is a JSON object having an <c>Id</c>
    /// property and a <c>Filters</c> array property. This array has an object
    /// for each filter, with <c>Id</c> and eventually <c>Options</c> (a custom
    /// schema object).
    /// </summary>
    /// <seealso cref="ComponentFactoryBase" />
    public sealed class EmbixFilterFactory : ComponentFactoryBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EmbixFilterFactory"/> class.
        /// </summary>
        /// <param name="container">The container.</param>
        /// <param name="configuration">The configuration.</param>
        public EmbixFilterFactory(Container container, IConfiguration configuration)
            : base(container, configuration)
        {
        }

        /// <summary>
        /// Configures the container services to use components from
        /// <c>Embix.Core</c>.
        /// This is just a helper method: at any rate, the configuration of
        /// the container is external to the VSM factory. You could use this
        /// method as a model and create your own, or call this method to
        /// register the components from these two assemblies, and then
        /// further configure the container, or add more assemblies when
        /// calling this via <paramref name="additionalAssemblies"/>.
        /// </summary>
        /// <param name="container">The container.</param>
        /// <param name="additionalAssemblies">The optional additional
        /// assemblies.</param>
        /// <exception cref="ArgumentNullException">container</exception>
        public static void ConfigureServices(Container container,
            params Assembly[] additionalAssemblies)
        {
            if (container == null)
                throw new ArgumentNullException(nameof(container));

            // https://simpleinjector.readthedocs.io/en/latest/advanced.html?highlight=batch#batch-registration
            Assembly[] assemblies = new[]
            {
                // Embix.Core
                typeof(StandardTextFilter).Assembly,
            };
            if (additionalAssemblies?.Length > 0)
                assemblies = assemblies.Concat(additionalAssemblies).ToArray();

            container.Collection.Register<ITextFilter>(assemblies);
            container.Collection.Register<IStringTokenizer>(assemblies);
            container.Collection.Register<IStringTokenMultiplier>(assemblies);

            // required for injection
            container.RegisterInstance(new UniData());
        }

        /// <summary>
        /// Gets the text filters from <c>FilterChains/{chainId}/Filters</c>.
        /// </summary>
        /// <param name="chainId">The chain ID.</param>
        /// <returns>filters</returns>
        /// <exception cref="ArgumentNullException">chainId</exception>
        public IList<ITextFilter> GetTextFilters(string chainId)
        {
            if (chainId is null)
                throw new ArgumentNullException(nameof(chainId));

            IConfigurationSection section =
                Configuration.GetSection("FilterChains");
            if (!section.Exists()) return null;

            int index = 0;
            foreach (IConfigurationSection chainSection in section.GetChildren())
            {
                if (chainSection["Id"] == chainId)
                {
                    var entries = ComponentFactoryConfigEntry.ReadComponentEntries(
                        Configuration,
                        $"FilterChains:{index}:Filters");
                    return GetComponents<ITextFilter>(entries);
                }
                index++;
            }
            return new List<ITextFilter>();
        }

        /// <summary>
        /// Gets the tokenizer with the specified ID and all its filters.
        /// </summary>
        /// <param name="id">The tokenizer identifier.</param>
        /// <returns>The tokenizer with the filters from the specified chain.
        /// </returns>
        /// <exception cref="ArgumentNullException">tokenizerId</exception>
        public IStringTokenizer GetTokenizer(string id)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));

            IConfigurationSection section = Configuration.GetSection("Tokenizers");
                if (!section.Exists()) return null;

            int index = 0;
            foreach (IConfigurationSection tokSection in section.GetChildren())
            {
                // Tokenizer/Id
                if (tokSection["Id"] == id)
                {
                    // Tokenizer/TypeId, Options, FilterChain
                    string typeId = tokSection["TypeId"];

                    IStringTokenizer tokenizer = GetComponent<IStringTokenizer>(
                        typeId,
                        $"Tokenizers:{index}:Options",
                        true);
                    if (tokenizer == null) return null;

                    string chainId = tokSection["FilterChain"];
                    if (!string.IsNullOrEmpty(chainId))
                    {
                        tokenizer.Filter = new CompositeTextFilter(
                            GetTextFilters(chainId)?.ToArray());
                    }
                    return tokenizer;
                }
                index++;
            }
            return null;
        }

        /// <summary>
        /// Gets the token multiplier with the specified ID.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns>The multiplier, or null if not found.</returns>
        /// <exception cref="ArgumentNullException">id</exception>
        public IStringTokenMultiplier GetTokenMultiplier(string id)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));

            IConfigurationSection section = Configuration.GetSection("TokenMultipliers");
            if (!section.Exists()) return null;

            int index = 0;
            foreach (IConfigurationSection mulSection in section.GetChildren())
            {
                // Multiplier/Id
                if (mulSection["Id"] == id)
                {
                    // Multiplier/TypeId, Options
                    string typeId = mulSection["TypeId"];

                    return GetComponent<IStringTokenMultiplier>(
                        typeId,
                        $"TokenMultipliers:{index}:Options",
                        true);
                }
                index++;
            }
            return null;
        }
    }
}
