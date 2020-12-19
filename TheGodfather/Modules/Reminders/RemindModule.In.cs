﻿#region USING_DIRECTIVES
using System;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
#endregion

namespace TheGodfather.Modules.Reminders
{
    public partial class RemindModule
    {
        [Group("in")]
        [Description("Send a reminder after specific time span.")]

        public class RemindInModule : RemindModule
        {

            [GroupCommand, Priority(2)]
            public new Task ExecuteGroupAsync(CommandContext ctx,
                                             [Description("Time span until reminder.")] TimeSpan timespan,
                                             [Description("Channel to send message to.")] DiscordChannel channel,
                                             [RemainingText, Description("What to send?")] string message)
                => this.AddReminderAsync(ctx, timespan, channel, message);

            [GroupCommand, Priority(1)]
            public new Task ExecuteGroupAsync(CommandContext ctx,
                                             [Description("Channel to send message to.")] DiscordChannel channel,
                                             [Description("Time span until reminder.")] TimeSpan timespan,
                                             [RemainingText, Description("What to send?")] string message)
                => this.AddReminderAsync(ctx, timespan, channel, message);

            [GroupCommand, Priority(0)]
            public new Task ExecuteGroupAsync(CommandContext ctx,
                                             [Description("Time span until reminder.")] TimeSpan timespan,
                                             [RemainingText, Description("What to send?")] string message)
                => this.AddReminderAsync(ctx, timespan, null, message);
        }
    }
}
