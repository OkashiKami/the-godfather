﻿using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using TheGodfather.Database.Models;
using TheGodfather.Services.Common;

namespace TheGodfather.Services.Extensions
{
    public static class PeriodicTasksServiceExtensions
    {
        private static readonly Regex _urlRegex = new Regex(
            "<span> *<a +href *= *\"([^\"]+)\"> *\\[link\\] *</a> *</span>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );


        public static async Task<bool> SendFeedUpdateAsync(TheGodfatherBot shard, RssSubscription sub, SyndicationItem latest)
        {
            DiscordChannel? chn;
            try {
                chn = await shard.Client.GetShard(sub.GuildId).GetChannelAsync(sub.ChannelId);
            } catch (NotFoundException) {
                return false;
            }

            if (chn is null)
                return false;

            var emb = new LocalizedEmbedBuilder(shard.Services.GetRequiredService<LocalizationService>(), sub.GuildId);
            emb.WithTitle(latest.Title.Text);
            emb.WithUrl(sub.Feed.LastPostUrl);
            emb.WithColor(DiscordColor.Gold);
            emb.WithLocalizedTimestamp(latest.LastUpdatedTime > latest.PublishDate ? latest.LastUpdatedTime : latest.PublishDate);

            if (latest.Content is TextSyndicationContent content) {
                Match m = _urlRegex.Match(content.Text);
                string? imageUrl = m.Success ? m.Groups[1].Value : null;
                if (imageUrl is { })
                    emb.WithImageUrl(imageUrl);
            }

            if (!string.IsNullOrWhiteSpace(sub.Name))
                emb.AddLocalizedTitleField("str-from", sub.Name);
            emb.AddLocalizedTitleField("str-content", sub.Feed.LastPostUrl);

            await chn.SendMessageAsync(embed: emb.Build());
            return true;
        }
    }
}
