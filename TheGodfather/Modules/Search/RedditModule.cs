﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using TheGodfather.Attributes;
using TheGodfather.Common;
using TheGodfather.Database.Models;
using TheGodfather.Exceptions;
using TheGodfather.Extensions;
using TheGodfather.Modules.Search.Common;
using TheGodfather.Modules.Search.Exceptions;
using TheGodfather.Modules.Search.Extensions;
using TheGodfather.Modules.Search.Services;

namespace TheGodfather.Modules.Search
{
    [Group("reddit"), Module(ModuleType.Searches), NotBlocked]
    [Aliases("r")]
    [Cooldown(3, 5, CooldownBucketType.Channel)]
    public sealed class RedditModule : TheGodfatherServiceModule<RedditService>
    {
        #region reddit
        [GroupCommand]
        public Task ExecuteGroupAsync(CommandContext ctx,
                                     [Description("desc-sub")] string sub = "all")
            => this.InternalSearchAsync(ctx, sub, RedditCategory.Hot);
        #endregion

        #region reddit controversial
        [Command("controversial")]
        [Description("Get newest controversial posts for a subreddit.")]
        [Aliases("c")]
        public Task ControversialAsync(CommandContext ctx,
                                      [Description("desc-sub")] string sub)
            => this.InternalSearchAsync(ctx, sub, RedditCategory.Controversial);
        #endregion

        #region reddit gilded
        [Command("gilded")]
        [Aliases("g")]
        public Task GildedAsync(CommandContext ctx,
                               [Description("desc-sub")] string sub)
            => this.InternalSearchAsync(ctx, sub, RedditCategory.Gilded);
        #endregion

        #region reddit hot
        [Command("hot")]
        [Aliases("h")]
        public Task HotAsync(CommandContext ctx,
                            [Description("desc-sub")] string sub)
            => this.InternalSearchAsync(ctx, sub, RedditCategory.Hot);
        #endregion

        #region reddit new
        [Command("new")]
        [Aliases("n", "newest", "latest")]
        public Task NewAsync(CommandContext ctx,
                            [Description("desc-sub")] string sub)
            => this.InternalSearchAsync(ctx, sub, RedditCategory.New);
        #endregion

        #region reddit rising
        [Command("rising")]
        [Aliases("r")]
        public Task RisingAsync(CommandContext ctx,
                               [Description("desc-sub")] string sub)
            => this.InternalSearchAsync(ctx, sub, RedditCategory.Rising);
        #endregion

        #region reddit top
        [Command("top")]
        [Aliases("t")]
        public Task TopAsync(CommandContext ctx,
                            [Description("desc-sub")] string sub)
            => this.InternalSearchAsync(ctx, sub, RedditCategory.Top);
        #endregion
        
        #region reddit subscribe
        [Command("subscribe"), Priority(1)]
        [Aliases("sub", "follow")]
        [RequireGuild, RequireUserPermissions(Permissions.ManageGuild)]
        public async Task SubscribeAsync(CommandContext ctx,
                                        [Description("desc-chn")] DiscordChannel chn,
                                        [Description("desc-sub")] string sub)
        {
            chn ??= ctx.Channel;
            if (chn.Type != ChannelType.Text)
                throw new InvalidCommandUsageException(ctx, "cmd-err-chn-type-text");

            if (string.IsNullOrWhiteSpace(sub))
                throw new InvalidCommandUsageException(ctx, "cmd-err-sub-none");

            string? url = this.Service.GetFeedURLForSubreddit(sub, RedditCategory.New, out string? rsub);
            if (url is null || rsub is null) {
                if (rsub is null)
                    throw new CommandFailedException(ctx, "cmd-err-sub-format");
                else
                    throw new CommandFailedException(ctx, "cmd-err-sub-404", rsub);
            }

            if (await ctx.Services.GetRequiredService<RssFeedsService>().SubscribeAsync(ctx.Guild.Id, ctx.Channel.Id, url, rsub))
                await ctx.InfoAsync(this.ModuleColor);
            else
                await ctx.FailAsync("cmd-err-sub", url);
        }

        public Task SubscribeAsync(CommandContext ctx,
                                  [Description("desc-sub")] string sub,
                                  [Description("desc-chn")] DiscordChannel? chn = null)
            => this.SubscribeAsync(ctx, chn ?? ctx.Channel, sub);
        #endregion

        #region reddit unsubscribe
        [Command("unsubscribe")]
        [Aliases("unfollow", "unsub")]
        [RequireGuild, RequireUserPermissions(Permissions.ManageGuild)]
        public async Task UnsubscribeAsync(CommandContext ctx,
                                          [Description("desc-sub")] string sub)
        {
            if (string.IsNullOrWhiteSpace(sub))
                throw new InvalidCommandUsageException(ctx, "cmd-err-sub-none");

            string? url = this.Service.GetFeedURLForSubreddit(sub, RedditCategory.New, out string? rsub);
            if (url is null || rsub is null) {
                if (rsub is null)
                    throw new CommandFailedException(ctx, "cmd-err-sub-format");
                else
                    throw new CommandFailedException(ctx, "cmd-err-sub-404", rsub);
            }

            RssFeedsService rss = ctx.Services.GetRequiredService<RssFeedsService>();
            RssFeed? feed = await rss.GetByUrlAsync(url);
            if (feed is null)
                throw new CommandFailedException(ctx, "cmd-err-sub-not");

            RssSubscription? s = await rss.Subscriptions.GetAsync((ctx.Guild.Id, ctx.Channel.Id), feed.Id);
            if (s is null)
                throw new CommandFailedException(ctx, "cmd-err-sub-not");

            await rss.Subscriptions.RemoveAsync((ctx.Guild.Id, ctx.Channel.Id), feed.Id);
            await ctx.InfoAsync(this.ModuleColor);
        }
        #endregion
        

        #region internals
        private async Task<IEnumerable<RedditPost>> InternalRetrieveResultsAsync(CommandContext ctx, string sub, RedditCategory category)
        {
            if (string.IsNullOrWhiteSpace(sub))
                throw new InvalidCommandUsageException(ctx, "cmd-err-sub-none");

            IEnumerable<RedditPost>? res;
            try {
                res = await this.Service.GetPostsAsync(sub, category);
            } catch (SearchServiceException<RedditError> e) {
                if (e.Details.ErrorCode == 404)
                    throw new CommandFailedException(ctx, "cmd-err-sub-404", sub);
                else
                    throw new CommandFailedException(ctx, "cmd-err-sub-fail-det", sub, e.Details.ErrorMessage);
            }

            if (res is null)
                throw new CommandFailedException(ctx, "cmd-err-sub-format");

            return res;
        }

        private async Task InternalSearchAsync(CommandContext ctx, string sub, RedditCategory category)
        {
            IEnumerable<RedditPost>? res = await this.InternalRetrieveResultsAsync(ctx, sub, category);

            if (!ctx.Channel.IsNsfwOrNsfwName() && res.Any(p => p.IsNsfw)) {
                await ctx.ImpInfoAsync(Emojis.Information, "cmd-err-sfw-only");
                res = res.Where(p => !p.IsNsfw);
            }

            if (!res.Any()) {
                await ctx.FailAsync("cmd-err-res-none");
                return;
            }

            await ctx.PaginateAsync(res, (emb, r) => emb.WithColor(this.ModuleColor).WithRedditPost(r), this.ModuleColor);
        }
        #endregion
    }
}
