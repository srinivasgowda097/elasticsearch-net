﻿using System;
using System.Collections.Generic;
using System.Linq;
using Bogus;
using Nest;
using Tests.Framework.Integration;
using Tests.Framework.MockData;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;
using Tests.Framework;

namespace Tests.Search.Request
{
	/**
	* Allows to highlight search results on one or more fields.
	* The implementation uses either the lucene `highlighter`, `fast-vector-highlighter` or `postings-highlighter`.
	*
	* See the Elasticsearch documentation on {ref_current}/search-request-highlighting.html[highlighting] for more detail.
	*/
	public class HighlightingUsageTests : SearchUsageTestBase
	{
		public HighlightingUsageTests(ReadOnlyCluster cluster, EndpointUsage usage) : base(cluster, usage) { }

		protected override object ExpectJson => new
		{
			query = new
			{
				match = new JObject
				{
					{ "name.standard", new JObject
						{
							{ "query", "Upton Sons Shield Rice Rowe Roberts" }
						}
					}
				}
			},
			highlight = new
			{
				pre_tags = new[] { "<tag1>" },
				post_tags = new[] { "</tag1>" },
				fields = new JObject
				{
					{ "name.standard", new JObject
						{
							{ "type", "plain" },
							{ "force_source", true},
							{ "fragment_size", 150 },
							{ "number_of_fragments", 3},
							{ "no_match_size", 150 }
						}
					},
					{ "leadDeveloper.firstName", new JObject
						{
							{ "type", "fvh" },
							{ "pre_tags", new JArray { "<name>" } },
							{ "post_tags", new JArray { "</name>" } },
							{ "highlight_query", new JObject
								{
									{ "match", new JObject
										{
											{ "leadDeveloper.firstName", new JObject
												{
													{ "query", "Kurt Edgardo Naomi Dariana Justice Felton" }
												}
											}
										}
									}
								}
							}
						}
					},
					{ "state.offsets", new JObject
						{
							{ "type", "postings" },
							{ "pre_tags", new JArray { "<state>" } },
							{ "post_tags", new JArray { "</state>" } },
							{ "highlight_query", new JObject
								{
									{ "terms", new JObject
										{
											{ "state.offsets", new JArray { "stable" , "bellyup" } }
										}
									}
								}
							}
						}
					}
				}
			}
		};

		protected override Func<SearchDescriptor<Project>, ISearchRequest> Fluent => s => s
			.Query(q => q
				.Match(m => m
					.Field(f => f.Name.Suffix("standard"))
					.Query("Upton Sons Shield Rice Rowe Roberts")
				)
			)
			.Highlight(h => h
				.PreTags("<tag1>")
				.PostTags("</tag1>")
				.Fields(
					fs => fs
						.Field(p => p.Name.Suffix("standard"))
						.Type("plain")
						.ForceSource()
						.FragmentSize(150)
						.NumberOfFragments(3)
						.NoMatchSize(150),
					fs => fs
						.Field(p => p.LeadDeveloper.FirstName)
						.Type(HighlighterType.Fvh)
						.PreTags("<name>")
						.PostTags("</name>")
						.HighlightQuery(q => q
							.Match(m => m
								.Field(p => p.LeadDeveloper.FirstName)
								.Query("Kurt Edgardo Naomi Dariana Justice Felton")
							)
						),
					fs => fs
						.Field(p => p.State.Suffix("offsets"))
						.Type(HighlighterType.Postings)
						.PreTags("<state>")
						.PostTags("</state>")
						.HighlightQuery(q => q
							.Terms(t => t
								.Field(f => f.State.Suffix("offsets"))
								.Terms(
									StateOfBeing.Stable.ToString().ToLowerInvariant(),
									StateOfBeing.BellyUp.ToString().ToLowerInvariant()
								)
							)
						)
				)
			);

		protected override SearchRequest<Project> Initializer =>
			new SearchRequest<Project>
			{
				Query = new MatchQuery
				{
					Query = "Upton Sons Shield Rice Rowe Roberts",
					Field = "name.standard"
				},
				Highlight = new Highlight
				{
					PreTags = new[] { "<tag1>" },
					PostTags = new[] { "</tag1>" },
					Fields = new Dictionary<Field, IHighlightField>
					{
						{ "name.standard", new HighlightField
							{
								Type = HighlighterType.Plain,
								ForceSource = true,
								FragmentSize = 150,
								NumberOfFragments = 3,
								NoMatchSize = 150
							}
						},
						{ "leadDeveloper.firstName", new HighlightField
							{
								TypeHack = "fvh",
								PreTags = new[] { "<name>"},
								PostTags = new[] { "</name>"},
								HighlightQuery = new MatchQuery
								{
									Field = "leadDeveloper.firstName",
									Query = "Kurt Edgardo Naomi Dariana Justice Felton"
								}
							}
						},
						{ "state.offsets", new HighlightField
							{
								Type = HighlighterType.Postings,
								PreTags = new[] { "<state>"},
								PostTags = new[] { "</state>"},
								HighlightQuery = new TermsQuery
								{
									Field = "state.offsets",
									Terms = new [] { "stable", "bellyup" }
								}
							}
						}
					}
				}
			};

		protected override void ExpectResponse(ISearchResponse<Project> response)
		{
			response.ShouldBeValid();

			foreach (var highlightsByDocumentId in response.Highlights)
			{
				foreach (var highlightHit in highlightsByDocumentId.Value)
				{
					if (highlightHit.Key == "name.standard")
					{
						foreach (var highlight in highlightHit.Value.Highlights)
						{
							highlight.Should().Contain("<tag1>");
							highlight.Should().Contain("</tag1>");
						}
					}
					else if (highlightHit.Key == "leadDeveloper.firstName")
					{
						foreach (var highlight in highlightHit.Value.Highlights)
						{
							highlight.Should().Contain("<name>");
							highlight.Should().Contain("</name>");
						}
					}
					else if (highlightHit.Key == "state.offsets")
					{
						foreach (var highlight in highlightHit.Value.Highlights)
						{
							highlight.Should().Contain("<state>");
							highlight.Should().Contain("</state>");
						}
					}
					else
					{
						Assert.True(false, $"highlights contains unexpected key {highlightHit.Key}");
					}
				}
			}
		}
	}
}
