﻿using System;
using System.Collections.Generic;
using Cowboy.Http;
using Cowboy.Http.Responses;
using Cowboy.Http.Routing;
using Cowboy.Http.Routing.Trie;
using Cowboy.Serialization;
using Cowboy.StaticContent;
using Cowboy.WebSockets;

namespace Cowboy
{
    public class Bootstrapper
    {
        public Bootstrapper()
        {
            this.Modules = new List<Module>();
        }

        public List<Module> Modules { get; set; }

        public Engine Boot()
        {
            var contextFactory = new ContextFactory();
            var staticContentProvider = BuildStaticContentProvider();
            var requestDispatcher = BuildRequestDispatcher();
            var webSocketDispatcher = new WebSocketDispatcher();

            return new Engine(contextFactory, staticContentProvider, requestDispatcher, webSocketDispatcher);
        }

        private StaticContentProvider BuildStaticContentProvider()
        {
            var rootPathProvider = new RootPathProvider();
            var staticContnetConventions = new StaticContentsConventions(new List<Func<Context, string, Response>>
            {
                StaticContentConventionBuilder.AddDirectory("Content")
            });
            var staticContentProvider = new StaticContentProvider(rootPathProvider, staticContnetConventions);

            FileResponse.SafePaths.Add(rootPathProvider.GetRootPath());

            return staticContentProvider;
        }

        private RequestDispatcher BuildRequestDispatcher()
        {
            var moduleCatalog = new ModuleCatalog();
            foreach (var module in Modules)
            {
                moduleCatalog.RegisterModule(module);
            }

            var routeSegmentExtractor = new RouteSegmentExtractor();
            var routeDescriptionProvider = new RouteDescriptionProvider();
            var routeCache = new RouteCache(routeSegmentExtractor, routeDescriptionProvider);
            routeCache.BuildCache(moduleCatalog.GetAllModules());

            var trieNodeFactory = new TrieNodeFactory();
            var routeTrie = new RouteResolverTrie(trieNodeFactory);
            routeTrie.BuildTrie(routeCache);

            var serializers = new List<ISerializer>() { new JsonSerializer(), new XmlSerializer() };
            var responseFormatterFactory = new ResponseFormatterFactory(serializers);
            var moduleBuilder = new ModuleBuilder(responseFormatterFactory);

            var routeResolver = new RouteResolver(moduleCatalog, moduleBuilder, routeTrie);

            var negotiator = new ResponseNegotiator();
            var routeInvoker = new RouteInvoker(negotiator);
            var requestDispatcher = new RequestDispatcher(routeResolver, routeInvoker);

            return requestDispatcher;
        }
    }
}