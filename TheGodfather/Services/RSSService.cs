﻿#region USING_DIRECTIVES
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

using TheGodfather.Common;

using DSharpPlus;
using DSharpPlus.Entities;
#endregion

namespace TheGodfather.Services
{
    public static class RSSService
    {
        private static Regex _subPrefixRegex = new Regex("^/?r?/", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static Regex _urlRegex = new Regex("<span> *<a +href *= *\"([^\"]+)\"> *\\[link\\] *</a> *</span>", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);


        public static bool IsValidRSSFeedURL(string url)
        {
            try {
                var feed = SyndicationFeed.Load(XmlReader.Create(url));
            } catch {
                return false;
            }
            return true;
        }

        public static string GetFeedURLForSubreddit(string sub, out string rsub)
        {
            sub = _subPrefixRegex.Replace(sub, "");
            rsub = "/r/" + sub.ToLowerInvariant();

            string url = $"https://www.reddit.com{rsub}/new/.rss";
            if (!IsValidRSSFeedURL(url))
                return null;

            return url;
        }

        public static IEnumerable<SyndicationItem> GetFeedResults(string url, int amount = 5)
        {
            try {
                using (var reader = XmlReader.Create(url)) {
                    var feed = SyndicationFeed.Load(reader);
                    return feed.Items.Take(amount);
                }
            } catch {
                return null;
            }
        }

        public static async Task CheckFeedsForChangesAsync(DiscordClient client, DBService db)
        {
            var feeds = await db.GetAllFeedEntriesAsync()
                .ConfigureAwait(false);
            foreach (var feed in feeds) {
                try {
                    if (!feed.Subscriptions.Any()) {
                        await db.RemoveFeedAsync(feed.Id)
                            .ConfigureAwait(false);
                        continue;
                    }

                    var newest = GetFeedResults(feed.URL)?.FirstOrDefault();
                    if (newest == null)
                        continue;

                    var url = newest.Links.FirstOrDefault()?.Uri.ToString();
                    if (url == null)
                        continue;

                    if (string.Compare(url, feed.SavedURL, true) != 0) {
                        await db.UpdateFeedSavedURLAsync(feed.Id, url)
                            .ConfigureAwait(false);
                        foreach (var sub in feed.Subscriptions) {
                            DiscordChannel chn;
                            try {
                                chn = await client.GetChannelAsync(sub.ChannelId)
                                    .ConfigureAwait(false);
                            } catch (Exception e) {
                                Logger.LogException(LogLevel.Warning, e);
                                await db.RemoveSubscriptionByIdAsync(sub.ChannelId, feed.Id)
                                    .ConfigureAwait(false);
                                continue;
                            }
                            var em = new DiscordEmbedBuilder() {
                                Title = $"{newest.Title.Text}",
                                Url = url,
                                Timestamp = newest.LastUpdatedTime,
                                Color = DiscordColor.White,
                            };

                            // FIXME reddit hack
                            if (newest.Content is TextSyndicationContent content) {
                                var matches = _urlRegex.Match(content.Text);
                                if (matches.Success)
                                    em.WithImageUrl(matches.Groups[1].Value);
                            }
                            if (!string.IsNullOrWhiteSpace(sub.QualifiedName))
                                em.AddField("From", sub.QualifiedName);
                            em.AddField("Link to content", url);
                            await chn.SendMessageAsync(embed: em.Build())
                                .ConfigureAwait(false);
                            await Task.Delay(100)
                                .ConfigureAwait(false);
                        }
                    }
                } catch (Exception e) {
                    Logger.LogException(LogLevel.Warning, e);
                }
            }
        }

        public static async Task SendFeedResultsAsync(DiscordChannel channel, IEnumerable<SyndicationItem> results)
        {
            if (results == null)
                return;

            var emb = new DiscordEmbedBuilder() {
                Title = "Topics active recently",
                Color = DiscordColor.White
            };

            foreach (var res in results)
                emb.AddField(res.Title.Text, res.Links.First().Uri.ToString());

            await channel.SendMessageAsync(embed: emb.Build())
                .ConfigureAwait(false);
        }
    }
}
