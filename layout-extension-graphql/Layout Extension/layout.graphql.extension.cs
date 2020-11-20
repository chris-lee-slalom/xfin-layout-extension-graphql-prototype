using GraphQL;
using GraphQL.Language.AST;
using Newtonsoft.Json.Linq;
using Sitecore;
using Sitecore.Abstractions;
using Sitecore.Data.Items;
using Sitecore.JavaScriptServices.Configuration;
using Sitecore.JavaScriptServices.GraphQL.Helpers;
using Sitecore.LayoutService.Configuration;
using Sitecore.LayoutService.ItemRendering.ContentsResolvers;
using Sitecore.Services.GraphQL.Abstractions;
using Sitecore.Services.GraphQL.Hosting;
using Sitecore.Services.GraphQL.Hosting.Configuration;
using Sitecore.Services.GraphQL.Hosting.Performance;
using Sitecore.Services.GraphQL.Hosting.QueryTransformation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace layout_extension_graphql.Layout_Extension
{
    public class GraphQLAwareRenderingContentsResolver : RenderingContentsResolver
    {
        private readonly IConfigurationResolver _configurationResolver;
        private readonly IDocumentWriter _documentWriter;
        private readonly BaseLog _log;
        private readonly IAsyncHelpers _asyncHelpers;
        private readonly Dictionary<string, IGraphQLEndpoint> _graphQLEndpoints;

        public const string PlaceholderPathKey = "PlaceholderPath";

        private JObject addPlaceHolderPath(Sitecore.Mvc.Presentation.Rendering rendering,
          IRenderingConfiguration renderingConfig)
        {
            var result = base.ResolveContents(rendering, renderingConfig) ?? new JObject();

            var jsonResult = (JObject)result;

            jsonResult[PlaceholderPathKey] = rendering.Placeholder;

            return jsonResult;
        }

        public GraphQLAwareRenderingContentsResolver(
              IConfigurationResolver configurationResolver,
              IGraphQLEndpointManager graphQLEndpointManager,
              IDocumentWriter documentWriter,
              BaseLog log,
              IAsyncHelpers asyncHelpers)
        {
            Sitecore.Diagnostics.Assert.ArgumentNotNull((object)configurationResolver, nameof(configurationResolver));
            Sitecore.Diagnostics.Assert.ArgumentNotNull((object)graphQLEndpointManager, nameof(graphQLEndpointManager));
            Sitecore.Diagnostics.Assert.ArgumentNotNull((object)documentWriter, nameof(documentWriter));
            Sitecore.Diagnostics.Assert.ArgumentNotNull((object)log, nameof(log));
            Sitecore.Diagnostics.Assert.ArgumentNotNull((object)asyncHelpers, nameof(asyncHelpers));
            this._configurationResolver = configurationResolver;
            this._documentWriter = documentWriter;
            this._log = log;
            this._asyncHelpers = asyncHelpers;
            this._graphQLEndpoints = graphQLEndpointManager.GetEndpoints().ToDictionary<IGraphQLEndpoint, string, IGraphQLEndpoint>((Func<IGraphQLEndpoint, string>)(endpoint => endpoint.Url), (Func<IGraphQLEndpoint, IGraphQLEndpoint>)(endpoint => endpoint), (IEqualityComparer<string>)StringComparer.OrdinalIgnoreCase);
        }

        public override object ResolveContents(
          Sitecore.Mvc.Presentation.Rendering rendering,
          IRenderingConfiguration renderingConfig)
        {
            RenderingItem renderingItem = rendering.RenderingItem;

            if (renderingItem == null)
            {
                return addPlaceHolderPath(rendering, renderingConfig);
                return base.ResolveContents(rendering, renderingConfig);
            }

            string str = renderingItem.InnerItem[Sitecore.JavaScriptServices.Core.FieldIDs.JsonRendering.GraphQLQuery];
            if (string.IsNullOrWhiteSpace(str))
            {
                return addPlaceHolderPath(rendering, renderingConfig);
                return base.ResolveContents(rendering, renderingConfig);
            }
            AppConfiguration appConfiguration = this._configurationResolver.ResolveForItem(Context.Item);
            if (appConfiguration == null)
            {
                this._log.Warn("[JSS] - Rendering " + renderingItem.InnerItem.Paths.FullPath + " defined a GraphQL query to resolve its data, but when rendered on item " + Context.Item.Paths.FullPath + " it was not within a known JSS app path. The GraphQL query will not be used.", (object)this);
                return addPlaceHolderPath(rendering, renderingConfig);
                return base.ResolveContents(rendering, renderingConfig);
            }
            if (string.IsNullOrWhiteSpace(appConfiguration.GraphQLEndpoint))
            {
                this._log.Error("[JSS] - The JSS app " + appConfiguration.Name + " did not have a graphQLEndpoint set, but rendering " + renderingItem.InnerItem.Paths.FullPath + " defined a GraphQL query to resolve its data. The GraphQL query will not be used until an endpoint is defined on the app config.", (object)this);
                return addPlaceHolderPath(rendering, renderingConfig);
                return base.ResolveContents(rendering, renderingConfig);
            }
            IGraphQLEndpoint graphQlEndpoint;
            if (!this._graphQLEndpoints.TryGetValue(appConfiguration.GraphQLEndpoint, out graphQlEndpoint))
            {
                this._log.Error("[JSS] - The JSS app " + appConfiguration.Name + " is set to use GraphQL endpoint " + appConfiguration.GraphQLEndpoint + ", but no GraphQL endpoint was registered with this URL. GraphQL resolution will not be used.", (object)this);
                return addPlaceHolderPath(rendering, renderingConfig);
                return base.ResolveContents(rendering, renderingConfig);
            }
            GraphQLAwareRenderingContentsResolver.LocalGraphQLRequest localGraphQlRequest1 = new GraphQLAwareRenderingContentsResolver.LocalGraphQLRequest();
            localGraphQlRequest1.Query = str;
            GraphQLAwareRenderingContentsResolver.LocalGraphQLRequest localGraphQlRequest2 = localGraphQlRequest1;
            localGraphQlRequest2.LocalVariables.Add("contextItem", (object)Context.Item.ID.Guid.ToString());
            localGraphQlRequest2.LocalVariables.Add("datasource", (object)rendering.DataSource);
            localGraphQlRequest2.LocalVariables.Add("defaultID", HttpContext.Current.Request.QueryString["defaultID"] ?? string.Empty);
            localGraphQlRequest2.LocalVariables.Add("cardID", HttpContext.Current.Request.QueryString["cardID"] ?? string.Empty);
            IDocumentExecuter executor = graphQlEndpoint.CreateDocumentExecutor();
            ExecutionOptions options = graphQlEndpoint.CreateExecutionOptions((GraphQLRequest)localGraphQlRequest2, !HttpContext.Current.IsCustomErrorEnabled);
            if (options == null)
                throw new ArgumentException("Endpoint returned null options.");
            TransformationResult transformationResult = graphQlEndpoint.SchemaInfo.QueryTransformer.Transform((GraphQLRequest)localGraphQlRequest2);
            if (transformationResult.Errors != null)
                return (object)new ExecutionResult()
                {
                    Errors = transformationResult.Errors
                };
            options.Query = transformationResult.Document.OriginalQuery;
            options.Document = transformationResult.Document;
            if (options.Document.Operations.Any<Operation>((Func<Operation, bool>)(op => (uint)op.OperationType > 0U)))
                throw new InvalidOperationException("Cannot use mutations or subscriptions in a datasource query. Use queries only.");
            using (QueryTracer queryTracer = graphQlEndpoint.Performance.TrackQuery((GraphQLRequest)localGraphQlRequest2, options))
            {
                ExecutionResult result = this._asyncHelpers.RunSyncWithThreadContext<ExecutionResult>((Func<Task<ExecutionResult>>)(() => executor.ExecuteAsync(options)));
                graphQlEndpoint.Performance.CollectMetrics(graphQlEndpoint.SchemaInfo.Schema, (IEnumerable<Operation>)options.Document.Operations, result);
                new QueryErrorLog((ILogger)new BaseLogAdapter(this._log)).RecordQueryErrors(result);
                queryTracer.Result = result;
                //return (object)this._documentWriter.ToJObject((object)result);
                var jsonResultToConvert = (object)this._documentWriter.ToJObject((object)result);
                var jsonResult = (JObject)jsonResultToConvert;
                jsonResult[PlaceholderPathKey] = rendering.Placeholder;
                return jsonResult;
            }

        }

        protected class LocalGraphQLRequest : GraphQLRequest
        {
            public Inputs LocalVariables { get; } = new Inputs();

            public override Inputs GetVariables() => this.LocalVariables;
        }
    }
}