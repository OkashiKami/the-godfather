﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using DSharpPlus.Interactivity;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using TheGodfather.Common;
using TheGodfather.Exceptions;
using TheGodfather.Modules.Administration.Services;
using TheGodfather.Services;

namespace TheGodfather.Extensions
{
    internal static class CommandContextExtensions
    {
        public static string BuildInvocationDetailsString(this CommandContext ctx, string? reason = null)
        {
            LocalizationService ls = ctx.Services.GetRequiredService<LocalizationService>();
            string reason404 = ls.GetString(ctx.Guild?.Id, "rsn-none");
            return ls.GetString(ctx.Guild?.Id, "fmt-invocation-details", ctx.User, reason ?? reason404, ctx.Channel);
        }

        // TODO remove
        [Obsolete]
        public static async Task<DiscordUser?> WaitForGameOpponentAsync(this CommandContext ctx)
        {
            InteractivityResult<DiscordMessage> mctx = await ctx.Client.GetInteractivity().WaitForMessageAsync(
                xm => {
                    if (xm.Author.IsBot || xm.Author == ctx.User || xm.Channel != ctx.Channel)
                        return false;
                    string[] split = xm.Content.ToLowerInvariant().Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    return split.Length == 1 && (split[0] == "me" || split[0] == "i");
                }
            );

            return mctx.TimedOut ? null : mctx.Result.Author;
        }

        public static Task InformAsync(this CommandContext ctx, string? key = null, string? emoji = null, 
                                       bool important = true, DiscordColor? color = null)
            => InformAsync(ctx, emoji is null ? null : DiscordEmoji.FromName(ctx.Client, emoji), key, important, color);

        public static async Task InformAsync(this CommandContext ctx, DiscordEmoji? emoji = null, string? key = null, 
                                             bool important = true, DiscordColor? color = null)
        {
            ulong gid = ctx.Guild?.Id ?? 0;
            if (!important && (ctx.Services.GetService<GuildConfigService>().GetCachedConfig(gid)?.ReactionResponse ?? false)) {
                try {
                    await ctx.Message.CreateReactionAsync(emoji ?? Emojis.CheckMarkSuccess);
                } catch (NotFoundException) {
                    await InformAsync(ctx, "str-done");
                }
            } else {
                LocalizationService ls = ctx.Services.GetService<LocalizationService>();
                string response = ls.GetString(gid, string.IsNullOrWhiteSpace(key) ? "str-done!" : key);
                await ctx.RespondAsync(embed: new DiscordEmbedBuilder {
                    Description = $"{emoji ?? Emojis.CheckMarkSuccess} {response}",
                    Color = color ?? DiscordColor.Green
                });
            }
        }

        public static Task InformFailureAsync(this CommandContext ctx, string key, params object[]? args)
        {
            return ctx.RespondAsync(embed: new DiscordEmbedBuilder {
                Description = $"{Emojis.X} {ctx.Services.GetService<LocalizationService>().GetString(ctx.Guild?.Id ?? 0, key, args)}",
                Color = DiscordColor.IndianRed
            });
        }

        public static async Task<List<string>?> WaitAndParsePollOptionsAsync(this CommandContext ctx, string separator = ";")
        {
            InteractivityService interactivity = ctx.Services.GetService<InteractivityService>();
            interactivity.AddPendingResponse(ctx.Channel.Id, ctx.User.Id);

            InteractivityResult<DiscordMessage> mctx = await ctx.Client.GetInteractivity().WaitForMessageAsync(
                xm => xm.Author == ctx.User && xm.Channel == ctx.Channel
            );

            if (!interactivity.RemovePendingResponse(ctx.Channel.Id, ctx.User.Id))
                throw new ConcurrentOperationException(ctx, "err-concurrent-usr-rem");

            return mctx.TimedOut
                ? null
                : mctx.Result.Content.Split(separator, StringSplitOptions.RemoveEmptyEntries)
                .Distinct()
                .ToList();
        }

        public static Task SendCollectionInPagesAsync<T>(this CommandContext ctx, string title,
            IEnumerable<T> collection, Func<T, string> selector, DiscordColor? color = null, int pageSize = 10)
        {
            T[] arr = collection.ToArray();
            LocalizationService ls = ctx.Services.GetRequiredService<LocalizationService>();

            var pages = new List<Page>();
            int pageCount = (arr.Length - 1) / pageSize + 1;
            int from = 0;
            for (int i = 1; i <= pageCount; i++) {
                int to = from + pageSize > arr.Length ? arr.Length : from + pageSize;
                pages.Add(new Page(embed: new DiscordEmbedBuilder {
                    Title = ls.GetString(ctx.Guild?.Id, "fmt-page-title", title, i, pageCount),
                    Description = arr[from..to].Select(selector).Humanize("\n"),
                    Color = color ?? DiscordColor.Black
                }));
                from += pageSize;
            }

            return pages.Count > 1
                ? ctx.Client.GetInteractivity().SendPaginatedMessageAsync(ctx.Channel, ctx.User, pages)
                : ctx.Channel.SendMessageAsync(content: pages.First().Content, embed: pages.First().Embed);
        }

        public static async Task<bool> WaitForBoolReplyAsync(this CommandContext ctx, string key, DiscordChannel? channel = null, 
                                                             DiscordUser? user = null, bool reply = true, params object[]? args)
        {
            channel ??= ctx.Channel;
            user ??= ctx.User;
            LocalizationService ls = ctx.Services.GetRequiredService<LocalizationService>();

            await ctx.RespondAsync(embed: new DiscordEmbedBuilder {
                Description = $"{Emojis.Question} {ls.GetString(ctx.Guild?.Id, key, args)} (y/n)",
                Color = DiscordColor.Yellow
            });

            if (await ctx.Client.GetInteractivity().WaitForBoolReplyAsync(ctx))
                return true;

            if (reply)
                await channel.InformFailureAsync(ls.GetString(ctx.Guild?.Id, "str-aborting"));

            return false;
        }


        public static async Task<DiscordMessage?> WaitForDmReplyAsync(this CommandContext ctx,
                                                                                          DiscordDmChannel dm,
                                                                                          DiscordUser user,
                                                                                          TimeSpan? waitInterval = null)
        {
            InteractivityExtension interactivity = ctx.Client.GetInteractivity();
            InteractivityService interactivityService = ctx.Services.GetRequiredService<InteractivityService>();
            
            interactivityService.AddPendingResponse(ctx.Channel.Id, user.Id);
            InteractivityResult<DiscordMessage> mctx = await interactivity.WaitForMessageAsync(m => m.Channel == dm && m.Author == user, waitInterval);
            if (interactivityService is { } && !interactivityService.RemovePendingResponse(ctx.Channel.Id, user.Id))
                throw new ConcurrentOperationException(ctx, "err-concurrent-usr-rem");
            
            return mctx.TimedOut ? null : mctx.Result;
        }
    }
}
