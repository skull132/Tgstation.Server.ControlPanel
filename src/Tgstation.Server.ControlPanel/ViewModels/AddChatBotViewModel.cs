﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class AddChatBotViewModel : ViewModelBase, ITreeNode, ICommandReceiver<AddChatBotViewModel.AddChatBotCommand>
	{
		public enum AddChatBotCommand
		{
			Close,
			Add
		}

		public string Title => "Add Bot";

		public string Icon => "resm:Tgstation.Server.ControlPanel.Assets.plus.jpg";

		public bool IsExpanded { get; set; }

		public IReadOnlyList<ITreeNode> Children => null;

		public EnumCommand<AddChatBotCommand> Close { get; }
		public EnumCommand<AddChatBotCommand> Add { get; }

		public ChatProvider Provider
		{
			get => provider;
			set
			{
				this.RaiseAndSetIfChanged(ref provider, value);
				this.RaisePropertyChanged(nameof(IrcSelected));
				this.RaisePropertyChanged(nameof(DiscordSelected));
				Add.Recheck();
			}
		}

		public bool IrcSelected => Provider == ChatProvider.Irc;
		public bool DiscordSelected => Provider == ChatProvider.Discord;

		public bool IrcUseSsl { get; set; }
		public string IrcServer
		{
			get => ircServer;
			set
			{
				this.RaiseAndSetIfChanged(ref ircServer, value);
				Add.Recheck();
			}
		}

		public ushort IrcPort { get; set; }
		public string IrcPassword
		{
			get => ircPassword;
			set
			{
				this.RaiseAndSetIfChanged(ref ircPassword, value);
				Add.Recheck();
			}
		}

		public int IrcPasswordType
		{
			get => ircPasswordType;
			set
			{
				this.RaiseAndSetIfChanged(ref ircPasswordType, value);
				this.RaisePropertyChanged(nameof(IrcUsingPassword));
				if (!IrcUsingPassword)
				{
					IrcPassword = string.Empty;
					this.RaisePropertyChanged(nameof(IrcPassword));
				}
				Add.Recheck();
			}
		}

		public int DiscordDMDisplay
		{
			get => discordDmDisplay;
			set => this.RaiseAndSetIfChanged(ref discordDmDisplay, value);
		}

		public bool IrcUsingPassword => IrcPasswordType != 3;

		public string DiscordBotToken
		{
			get => discordBotToken;
			set
			{
				this.RaiseAndSetIfChanged(ref discordBotToken, value);
				Add.Recheck();
			}
		}

		public bool DiscordMeme
		{
			get => discordMeme;
			set => this.RaiseAndSetIfChanged(ref discordMeme, value);
		}

		public bool Enabled
		{
			get => enabled;
			set => this.RaiseAndSetIfChanged(ref enabled, value);
		}
		public string BotName
		{
			get => botName;
			set
			{
				this.RaiseAndSetIfChanged(ref botName, value);
				Add.Recheck();
			}
		}
		public string IrcNick
		{
			get => ircNick;
			set
			{
				this.RaiseAndSetIfChanged(ref ircNick, value);
				Add.Recheck();
			}
		}

		readonly PageContextViewModel pageContext;
		readonly IChatBotsClient chatBotsClient;
		readonly IInstanceUserRightsProvider rightsProvider;
		readonly ChatRootViewModel chatRootViewModel;

		ChatProvider provider;
		bool loading;
		string botName;
		bool enabled;
		bool discordMeme;
		int discordDmDisplay;
		int ircPasswordType;
		string ircPassword;
		string ircServer;
		string ircNick;
		string discordBotToken;

		public AddChatBotViewModel(PageContextViewModel pageContext, IChatBotsClient chatBotsClient, IInstanceUserRightsProvider rightsProvider, ChatRootViewModel chatRootViewModel)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.chatBotsClient = chatBotsClient ?? throw new ArgumentNullException(nameof(chatBotsClient));
			this.rightsProvider = rightsProvider ?? throw new ArgumentNullException(nameof(rightsProvider));
			this.chatRootViewModel = chatRootViewModel ?? throw new ArgumentNullException(nameof(chatRootViewModel));
			rightsProvider.OnUpdated += (a, b) => Add.Recheck();
			Close = new EnumCommand<AddChatBotCommand>(AddChatBotCommand.Close, this);
			Add = new EnumCommand<AddChatBotCommand>(AddChatBotCommand.Add, this);
			IrcPasswordType = 3;
			IrcPort = 6667;
			DiscordMeme = true;
		}

		public Task HandleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}

		public bool CanRunCommand(AddChatBotCommand command)
		{
			return command switch
			{
				AddChatBotCommand.Close => true,
				AddChatBotCommand.Add => !loading
					&& !string.IsNullOrEmpty(BotName)
					&& rightsProvider.ChatBotRights.HasFlag(ChatBotRights.Create)
					&& ((DiscordSelected && !string.IsNullOrEmpty(DiscordBotToken))
						|| (IrcSelected && !string.IsNullOrEmpty(IrcServer)
						&& !string.IsNullOrEmpty(IrcNick)
						&& (!IrcUsingPassword
						|| !string.IsNullOrEmpty(IrcPassword)))),
				_ => throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!"),
			};
		}

		public async Task RunCommand(AddChatBotCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case AddChatBotCommand.Close:
					pageContext.ActiveObject = null;
					break;
				case AddChatBotCommand.Add:
					loading = true;
					Add.Recheck();
					try
					{
						var newBot = new ChatBot
						{
							Provider = Provider,
							Name = BotName,
							Enabled = Enabled,
							Channels = null
						};

						switch (Provider)
						{
							case ChatProvider.Discord:
								newBot.SetConnectionStringBuilder(new DiscordConnectionStringBuilder
								{
									BotToken = DiscordBotToken,
									BasedMeme = DiscordMeme,
									DMOutputDisplay = (DiscordDMOutputDisplayType)DiscordDMDisplay
								});
								break;
							case ChatProvider.Irc:
								newBot.SetConnectionStringBuilder(new IrcConnectionStringBuilder
								{
									Address = IrcServer,
									Port = IrcPort,
									Nickname = IrcNick,
									Password = IrcUsingPassword ? IrcPassword : null,
									PasswordType = IrcUsingPassword ? (IrcPasswordType?)IrcPasswordType : null,
									UseSsl = IrcUseSsl
								});
								break;
							default:
								throw new InvalidOperationException("Invalid Provider!");
						}

						newBot = await chatBotsClient.Create(newBot, cancellationToken).ConfigureAwait(true);
						chatRootViewModel.DirectAdd(newBot);
					}
					finally
					{
						loading = false;
						Add.Recheck();
					}
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
	}
}