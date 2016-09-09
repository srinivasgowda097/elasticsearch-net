﻿using System;
using System.Collections.Generic;
using Elasticsearch.Net;
using FluentAssertions;
using Nest;
using Tests.Framework;
using Tests.Framework.Integration;
using Tests.Framework.MockData;
using Xunit;

namespace Tests.QueryDsl
{
	public abstract class QueryDslUsageTestsBase : ApiTestBase<ReadOnlyCluster, ISearchResponse<Project>, ISearchRequest, SearchDescriptor<Project>, SearchRequest<Project>>
	{
		protected QueryDslUsageTestsBase(ClusterBase cluster, EndpointUsage usage) : base(cluster, usage) { }
		protected override LazyResponses ClientUsage() => Calls(
			fluent: (client, f) => client.Search<Project>(f),
			fluentAsync: (client, f) => client.SearchAsync<Project>(f),
			request: (client, r) => client.Search<Project>(r),
			requestAsync: (client, r) => client.SearchAsync<Project>(r)
		);

		protected override HttpMethod HttpMethod => HttpMethod.POST;
		protected override string UrlPath => "/project/project/_search";

		protected abstract object QueryJson { get; }

		protected abstract QueryContainer QueryInitializer { get; }
		protected abstract QueryContainer QueryFluent(QueryContainerDescriptor<Project> q);

		protected override object ExpectJson => new { query = this.QueryJson };

		[U] public void FluentIsNotConditionless() =>
			AssertIsNotConditionless(this.QueryFluent(new QueryContainerDescriptor<Project>()));

		[U] public void InitializerIsNotConditionless() => AssertIsNotConditionless(this.QueryInitializer);

		private void AssertIsNotConditionless(IQueryContainer c)
		{
			if (!c.IsVerbatim)
				c.IsConditionless.Should().BeFalse();
		}

		protected override Func<SearchDescriptor<Project>, ISearchRequest> Fluent => s => s
			.Query(this.QueryFluent);

		protected override SearchRequest<Project> Initializer =>
			new SearchRequest<Project>
			{
				Query = this.QueryInitializer
			};

		protected virtual NotConditionlessWhen NotConditionlessWhen => null;
		protected virtual ConditionlessWhen ConditionlessWhen => null;

		protected QueryContainer ConditionlessQuery = new QueryContainer(new TermQuery { });

		protected QueryContainer VerbatimQuery = new QueryContainer(new TermQuery { IsVerbatim = true });

		[U] public void SeenByVisitor()
		{
			var visitor = new DslPrettyPrintVisitor(TestClient.GlobalDefaultSettings);
			var query = this.QueryFluent(new QueryContainerDescriptor<Project>());
			query.Accept(visitor);
			var pretty = visitor.PrettyPrint;
			pretty.Should().NotBeNullOrWhiteSpace();
		}


		[U] public void ConditionlessWhenExpectedToBe()
		{
			if (ConditionlessWhen == null) return;
			foreach (var when in ConditionlessWhen)
			{
				var query = when(this.QueryFluent(new QueryContainerDescriptor<Project>()));
				//this.JsonEquals(query, new { });
				query = when(this.QueryInitializer);
				//this.JsonEquals(query, new { });
			}

			((IQueryContainer)this.QueryInitializer).IsConditionless.Should().BeFalse();
		}

		[U] public void NotConditionlessWhenExpectedToBe()
		{
			if (NotConditionlessWhen == null) return;
			foreach (var when in NotConditionlessWhen)
			{
				var query = this.QueryFluent(new QueryContainerDescriptor<Project>());
				when(query);

				query = this.QueryInitializer;
				when(query);
			}
		}

		private void IsConditionless(IQueryContainer q, bool be) => q.IsConditionless.Should().Be(be);
	}

	public abstract class ConditionlessWhen : List<Func<QueryContainer, QueryContainer>>
	{
	}
	public class ConditionlessWhen<TQuery> : ConditionlessWhen where TQuery : IQuery
	{
		private readonly Func<IQueryContainer, TQuery> _dispatch;

		public ConditionlessWhen(Func<IQueryContainer, TQuery> dispatch)
		{
			_dispatch = dispatch;
		}

		public void Add(Action<TQuery> when)
		{
			this.Add(q => Assert(q, when));
		}

		private QueryContainer Assert(IQueryContainer c, Action<TQuery> when)
		{
			TQuery q = this._dispatch(c);
			q.Conditionless.Should().BeFalse();
			c.IsConditionless.Should().BeFalse();
			when(q);
			q.Conditionless.Should().BeTrue();
			c.IsConditionless.Should().BeTrue();
			return (QueryContainer)c;
		}
	}

	public abstract class NotConditionlessWhen : List<Action<QueryContainer>>
	{
	}
	public class NotConditionlessWhen<TQuery> : NotConditionlessWhen where TQuery : IQuery
	{
		private readonly Func<IQueryContainer, TQuery> _dispatch;

		public NotConditionlessWhen(Func<IQueryContainer, TQuery> dispatch)
		{
			_dispatch = dispatch;
		}

		public void Add(Action<TQuery> when)
		{
			this.Add(q => Assert(q, when));
		}

		private void Assert(IQueryContainer c, Action<TQuery> when)
		{
			TQuery q = this._dispatch(c);
			when(q);
			q.Conditionless.Should().BeFalse();
			c.IsConditionless.Should().BeFalse();
		}
	}
}
