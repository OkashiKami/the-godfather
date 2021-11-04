﻿using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using TheGodfather.Attributes;
using TheGodfather.Common;
using TheGodfather.Exceptions;
using TheGodfather.Extensions;
using TheGodfather.Modules.Music.Common;
using TheGodfather.Modules.Music.Services;
using TheGodfather.Modules.Owner.Services;

namespace TheGodfather.Modules.Music
{
    [Group("music"), Module(ModuleType.Music), NotBlocked]
    [Aliases("songs", "song", "tracks", "track", "audio", "mu")]
    [RequireGuild]
    [Cooldown(3, 5, CooldownBucketType.Guild)]
    [ModuleLifespan(ModuleLifespan.Transient)]
    public sealed partial class MusicModule : TheGodfatherServiceModule<MusicService>
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private GuildMusicPlayer Player { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.


        #region pre-execution
        public override async Task BeforeExecutionAsync(CommandContext ctx)
        {
            if (this.Service.IsDisabled)
                throw new ServiceDisabledException(ctx);

            if (!ctx.Client.IsOwnedBy(ctx.User) && !await ctx.Services.GetRequiredService<PrivilegedUserService>().ContainsAsync(ctx.User.Id)) {
                DiscordVoiceState? memberVoiceState = ctx.Member.VoiceState;
                DiscordChannel? chn = memberVoiceState?.Channel;
                if (chn is null)
                    throw new CommandFailedException(ctx, "cmd-err-music-vc");

                DiscordChannel? botVoiceState = ctx.Guild.CurrentMember?.VoiceState?.Channel;
                if (botVoiceState is { } && chn != botVoiceState)
                    throw new CommandFailedException(ctx, "cmd-err-music-vc-same");

                if (!chn.PermissionsFor(ctx.Guild.CurrentMember).HasPermission(Permissions.AccessChannels))
                    throw new ChecksFailedException(ctx.Command, ctx, new[] { new RequireBotPermissionsAttribute(Permissions.Speak) });
            }

            this.Player = await this.Service.GetOrCreateDataAsync(ctx.Guild);
            this.Player.CommandChannel = ctx.Channel;

            await base.BeforeExecutionAsync(ctx);
        }
        #endregion


        #region music
        [GroupCommand]
        public Task ExecuteGroupAsync(CommandContext ctx)
        {
            Song song = this.Player.NowPlaying;
            if (string.IsNullOrWhiteSpace(this.Player.NowPlaying.Track?.TrackString))
                return ctx.ImpInfoAsync(this.ModuleColor, Emojis.Headphones, "str-music-none");

            return ctx.RespondWithLocalizedEmbedAsync(emb => {
                emb.WithLocalizedTitle("str-music-queue");
                emb.WithColor(this.ModuleColor);
                emb.WithDescription(Formatter.Bold(Formatter.Sanitize(song.Track.Title)));
                emb.AddLocalizedTitleField("str-author", song.Track.Author, inline: true);
                emb.AddLocalizedTitleField("str-duration", song.Track.Length.ToDurationString(), inline: true);
                emb.AddLocalizedTitleField("str-requested-by", song.RequestedBy?.Mention, inline: true);
            });
        }
        #endregion

        #region music forward
        [Command("forward")]
        [Aliases("fw", "f", ">", ">>")]
        public async Task ForwardAsync(CommandContext ctx,
                                      [RemainingText, Description("desc-music-fw")] TimeSpan offset)
        {
            await this.Player.SeekAsync(offset, true);
            await ctx.InfoAsync(this.ModuleColor);
        }
        #endregion

        #region music info
        [Command("info")]
        [Aliases("i", "player")]
        public Task PlayerInfoAsync(CommandContext ctx)
        {
            return ctx.RespondWithLocalizedEmbedAsync(emb => {
                emb.WithLocalizedTitle("str-music-player");
                emb.WithColor(this.ModuleColor);
                var totalQueueTime = TimeSpan.FromSeconds(this.Player.Queue.Sum(s => s.Track.Length.TotalSeconds));
                emb.AddLocalizedTitleField("str-music-shuffled", this.Player.IsShuffled, inline: true);
                emb.AddLocalizedTitleField("str-music-mode", this.Player.RepeatMode, inline: true);
                emb.AddLocalizedTitleField("str-music-vol", $"{this.Player.Volume}%", inline: true);
                emb.AddLocalizedTitleField("str-music-queue-len", $"{this.Player.Queue.Count} ({totalQueueTime.ToDurationString()})", inline: true);
            });
        }
        #endregion

        #region music pause
        [Command("pause")]
        [Aliases("ps")]
        public async Task PauseAsync(CommandContext ctx)
        {
            if (!this.Player.IsPlaying) {
                await this.ResumeAsync(ctx);
                return;
            }

            await this.Player.PauseAsync();
            await ctx.InfoAsync(this.ModuleColor, Emojis.Headphones, "str-music-pause");
        }
        #endregion

        #region music repeat
        [Command("repeat")]
        [Aliases("loop", "l", "rep", "lp")]
        public Task RepeatAsync(CommandContext ctx,
                               [Description("desc-music-mode")] RepeatMode mode = RepeatMode.Single)
        {
            this.Player.SetRepeatMode(mode);
            return ctx.InfoAsync(this.ModuleColor, Emojis.Headphones, "fmt-music-mode", mode);
        }
        #endregion

        #region music restart
        [Command("restart")]
        [Aliases("res", "replay")]
        public async Task RestartAsync(CommandContext ctx)
        {
            Song song = this.Player.NowPlaying;
            await this.Player.RestartAsync();
            await ctx.InfoAsync(this.ModuleColor, Emojis.Headphones, "fmt-music-replay", Formatter.Sanitize(song.Track.Title), Formatter.Sanitize(song.Track.Author));
        }
        #endregion

        #region music resume
        [Command("resume")]
        [Aliases("unpause", "up", "rs")]
        public async Task ResumeAsync(CommandContext ctx)
        {
            await this.Player.ResumeAsync();
            await ctx.InfoAsync(this.ModuleColor, Emojis.Headphones, "str-music-resume");
        }
        #endregion

        #region music reshuffle
        [Command("reshuffle")]
        public Task ReshuffleAsync(CommandContext ctx)
        {
            this.Player.Reshuffle();
            return ctx.InfoAsync(this.ModuleColor, Emojis.Headphones, "str-music-reshuffle");
        }
        #endregion

        #region music rewind
        [Command("rewind")]
        [Aliases("bw", "rw", "<", "<<")]
        public async Task RewindAsync(CommandContext ctx,
                                     [RemainingText, Description("desc-music-bw")] TimeSpan offset)
        {
            await this.Player.SeekAsync(-offset, true);
            await ctx.InfoAsync(this.ModuleColor);
        }
        #endregion

        #region music seek
        [Command("seek")]
        [Aliases("s")]
        public async Task SeekAsync(CommandContext ctx,
                                   [RemainingText, Description("desc-music-seek")] TimeSpan position)
        {
            await this.Player.SeekAsync(position, false);
            await ctx.InfoAsync(this.ModuleColor);
        }
        #endregion

        #region music shuffle
        [Command("shuffle")]
        [Aliases("randomize", "rng", "sh")]
        public Task ShuffleAsync(CommandContext ctx)
        {
            if (this.Player.IsShuffled) {
                this.Player.StopShuffle();
                return ctx.InfoAsync(this.ModuleColor, Emojis.Headphones, "str-music-unshuffle");
            } else {
                this.Player.Shuffle();
                return ctx.InfoAsync(this.ModuleColor, Emojis.Headphones, "str-music-shuffle");
            }
        }
        #endregion

        #region music skip
        [Command("skip")]
        [Aliases("next", "n", "sk")]
        public async Task SkipAsync(CommandContext ctx)
        {
            Song song = this.Player.NowPlaying;
            await this.Player.StopAsync();
            await ctx.InfoAsync(this.ModuleColor, Emojis.Headphones, "fmt-music-skip", Formatter.Sanitize(song.Track.Title), Formatter.Sanitize(song.Track.Author));
        }
        #endregion

        #region music stop
        [Command("stop")]
        public async Task StopAsync(CommandContext ctx)
        {
            int removed = this.Player.EmptyQueue();
            await this.Player.StopAsync();
            await this.Player.DestroyPlayerAsync();
            await ctx.InfoAsync(this.ModuleColor, Emojis.Headphones, "fmt-music-del-many", removed);
        }
        #endregion

        #region music volume
        [Command("volume")]
        [Aliases("vol", "v")]
        public async Task VolumeAsync(CommandContext ctx,
                                     [Description("desc-music-vol")] int volume = 100)
        {
            if (volume < GuildMusicPlayer.MinVolume || volume > GuildMusicPlayer.MaxVolume)
                throw new InvalidCommandUsageException(ctx, "cmd-err-music-vol", GuildMusicPlayer.MinVolume, GuildMusicPlayer.MaxVolume);

            await this.Player.SetVolumeAsync(volume);
            await ctx.InfoAsync(this.ModuleColor, Emojis.Headphones, "fmt-music-vol", volume);
        }
        #endregion
    }
}
