﻿using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class RepositoryViewModel : ViewModelBase, ITreeNode, ICommandReceiver<RepositoryViewModel.RepositoryCommand>
	{
		public enum RepositoryCommand
		{
			Close,
			Refresh,
			Clone,
			Delete,
			Update,
			RemoveCredentials,
			DirectAddPR,
			RefreshPRs
		}

		const string NoCredentials = "(not set)";

		public string Title => "Repository";

		public string Icon
		{
			get => icon;
			private set => this.RaiseAndSetIfChanged(ref icon, value);
		}

		public IReadOnlyList<ITreeNode> Children => null;

		public bool IsExpanded { get; set; }

		public Repository Repository
		{
			get => repository;
			set
			{
				this.RaiseAndSetIfChanged(ref repository, value);
				this.RaisePropertyChanged(nameof(Available));
				this.RaisePropertyChanged(nameof(HasCredentials));
			}
		}

		public bool Available => Repository.Origin != null;

		public string NewOrigin
		{
			get => newOrigin;
			set
			{
				this.RaiseAndSetIfChanged(ref newOrigin, value);
				Clone.Recheck();
			}
		}
		public string NewSha
		{
			get => newSha;
			set
			{
				this.RaiseAndSetIfChanged(ref newSha, value);
				this.RaisePropertyChanged(nameof(CanSetRef));
				this.RaisePropertyChanged(nameof(UpdateText));
			}
		}
		public string NewReference
		{
			get => newReference;
			set
			{
				this.RaiseAndSetIfChanged(ref newReference, value);
				this.RaisePropertyChanged(nameof(CanSetSha));
				this.RaisePropertyChanged(nameof(UpdateText));
			}
		}
		public string NewCommitterName
		{
			get => newCommitterName;
			set => this.RaiseAndSetIfChanged(ref newCommitterName, value);
		}
		public string NewCommitterEmail
		{
			get => newCommitterEmail;
			set => this.RaiseAndSetIfChanged(ref newCommitterEmail, value);
		}
		public string NewAccessUser
		{
			get => newAccessUser;
			set
			{
				this.RaiseAndSetIfChanged(ref newAccessUser, value);
				Update.Recheck();
				Clone.Recheck();
			}
		}
		public string NewAccessToken
		{
			get => newAccessToken;
			set
			{
				this.RaiseAndSetIfChanged(ref newAccessToken, value);
				Update.Recheck();
				Clone.Recheck();
			}
		}

		public bool NewUpdateFromOrigin
		{
			get => newUpdateFromOrigin ?? false;
			set
			{
				newUpdateFromOrigin = value ? (bool?)true : null;
				this.RaisePropertyChanged(nameof(newUpdateFromOrigin));
			}
		}

		public bool NewShowTestMergeCommitters
		{
			get => newShowTestMergeCommitters;
			set => this.RaiseAndSetIfChanged(ref newShowTestMergeCommitters, value);
		}

		public bool NewAutoUpdatesKeepTestMerges
		{
			get => newAutoUpdatesKeepTestMerges;
			set => this.RaiseAndSetIfChanged(ref newAutoUpdatesKeepTestMerges, value);
		}

		public bool NewAutoUpdatesSynchronize
		{
			get => newAutoUpdatesSynchronize;
			set => this.RaiseAndSetIfChanged(ref newAutoUpdatesSynchronize, value);
		}

		public bool CanClone => !Refreshing && rightsProvider.RepositoryRights.HasFlag(RepositoryRights.SetOrigin);
		public bool CanDelete => !Refreshing && rightsProvider.RepositoryRights.HasFlag(RepositoryRights.Delete);
		public bool CanSetRef => !Refreshing && rightsProvider.RepositoryRights.HasFlag(RepositoryRights.SetReference) && String.IsNullOrEmpty(NewSha);
		public bool CanSetSha => !Refreshing && rightsProvider.RepositoryRights.HasFlag(RepositoryRights.SetSha) && String.IsNullOrEmpty(NewReference);
		public bool CanShowTMCommitters => !Refreshing && rightsProvider.RepositoryRights.HasFlag(RepositoryRights.ChangeTestMergeCommits);
		public bool CanChangeCommitter => !Refreshing && rightsProvider.RepositoryRights.HasFlag(RepositoryRights.ChangeCommitter);
		public bool CanAccess => !Refreshing && rightsProvider.RepositoryRights.HasFlag(RepositoryRights.ChangeCredentials);
		public bool CanAutoUpdate => !Refreshing && rightsProvider.RepositoryRights.HasFlag(RepositoryRights.ChangeAutoUpdateSettings);
		public bool CanUpdate => !Refreshing && rightsProvider.RepositoryRights.HasFlag(RepositoryRights.UpdateBranch);
		public bool CanTestMerge => !Refreshing && rightsProvider.RepositoryRights.HasFlag(RepositoryRights.MergePullRequest);
		public bool CanDeploy => rightsProvider.DreamMakerRights.HasFlag(DreamMakerRights.Compile);

		public bool Error
		{
			get => error;
			set => this.RaiseAndSetIfChanged(ref error, value);
		}

		public bool CloneAvailable
		{
			get => cloneAvailable;
			set => this.RaiseAndSetIfChanged(ref cloneAvailable, value);
		}

		public bool Refreshing
		{
			get => refreshing;
			set => this.RaiseAndSetIfChanged(ref refreshing, value);
		}

		public string UpdateText => String.IsNullOrEmpty(NewSha) && String.IsNullOrEmpty(NewReference) ? "Fetch and Merge From Tracked Origin Reference" : "Fetch and Hard Reset to Target Origin Object";

		public string ErrorMessage
		{
			get => errorMessage;
			set => this.RaiseAndSetIfChanged(ref errorMessage, value);
		}

		public bool DeployAfter
		{
			get => deployAfter;
			set => this.RaiseAndSetIfChanged(ref deployAfter, value);
		}

		public IReadOnlyList<TestMergeViewModel> RemotePullRequests
		{
			get => remotePullRequests;
			set => this.RaiseAndSetIfChanged(ref remotePullRequests, value);
		}

		public IReadOnlyList<TestMergeViewModel> ActivePullRequests
		{
			get => activePullRequests;
			set => this.RaiseAndSetIfChanged(ref activePullRequests, value);
		}

		public bool HasCredentials => Repository?.AccessUser != null && Repository?.AccessUser != NoCredentials;

		public EnumCommand<RepositoryCommand> Close { get; }
		public EnumCommand<RepositoryCommand> RefreshCommand { get; }
		public EnumCommand<RepositoryCommand> Clone { get; }
		public EnumCommand<RepositoryCommand> Delete { get; }
		public EnumCommand<RepositoryCommand> Update { get; }
		public EnumCommand<RepositoryCommand> RemoveCredentials { get; }
		public EnumCommand<RepositoryCommand> DirectAddPR { get; }
		public EnumCommand<RepositoryCommand> RefreshPRs { get; }

		readonly PageContextViewModel pageContext;
		readonly IRepositoryClient repositoryClient;
		readonly IDreamMakerClient dreamMakerClient;
		readonly IInstanceJobSink jobSink;
		readonly IInstanceUserRightsProvider rightsProvider;
		
		Repository repository;

		IReadOnlyList<TestMergeViewModel> remotePullRequests;
		IReadOnlyList<TestMergeViewModel> activePullRequests;

		string newOrigin;
		string newSha;
		string newReference;
		string newCommitterName;
		string newCommitterEmail;
		string newAccessUser;
		string newAccessToken;

		bool? newUpdateFromOrigin;
		
		bool newShowTestMergeCommitters;
		bool newAutoUpdatesKeepTestMerges;
		bool newAutoUpdatesSynchronize;

		string errorMessage;
		bool error;

		string icon;

		bool refreshing;
		bool cloneAvailable;
		bool deployAfter;

		public RepositoryViewModel(PageContextViewModel pageContext, IRepositoryClient repositoryClient, IDreamMakerClient dreamMakerClient, IInstanceJobSink jobSink, IInstanceUserRightsProvider rightsProvider)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.repositoryClient = repositoryClient ?? throw new ArgumentNullException(nameof(repositoryClient));
			this.dreamMakerClient = dreamMakerClient ?? throw new ArgumentNullException(nameof(dreamMakerClient));
			this.jobSink = jobSink ?? throw new ArgumentNullException(nameof(jobSink));
			this.rightsProvider = rightsProvider ?? throw new ArgumentNullException(nameof(rightsProvider));

			Close = new EnumCommand<RepositoryCommand>(RepositoryCommand.Close, this);
			RefreshCommand = new EnumCommand<RepositoryCommand>(RepositoryCommand.Refresh, this);
			Clone = new EnumCommand<RepositoryCommand>(RepositoryCommand.Clone, this);
			Delete = new EnumCommand<RepositoryCommand>(RepositoryCommand.Delete, this);
			Update = new EnumCommand<RepositoryCommand>(RepositoryCommand.Update, this);
			RemoveCredentials = new EnumCommand<RepositoryCommand>(RepositoryCommand.RemoveCredentials, this);
			DirectAddPR = new EnumCommand<RepositoryCommand>(RepositoryCommand.DirectAddPR, this);
			RefreshPRs = new EnumCommand<RepositoryCommand>(RepositoryCommand.RefreshPRs, this);

			rightsProvider.OnUpdated += (a, b) => RecheckCommands();

			async void InitialLoad() => await Refresh(null, null, default).ConfigureAwait(false);
			if (CanRunCommand(RepositoryCommand.Refresh))
				InitialLoad();
		}

		void RecheckCommands()
		{
			RefreshCommand.Recheck();
			Delete.Recheck();
			Clone.Recheck();
			Update.Recheck();

			this.RaisePropertyChanged(nameof(CanAutoUpdate));
			this.RaisePropertyChanged(nameof(CanChangeCommitter));
			this.RaisePropertyChanged(nameof(CanAccess));
			this.RaisePropertyChanged(nameof(CanShowTMCommitters));
			this.RaisePropertyChanged(nameof(CanTestMerge));
			this.RaisePropertyChanged(nameof(CanSetRef));
			this.RaisePropertyChanged(nameof(CanSetSha));
			this.RaisePropertyChanged(nameof(CanUpdate));
			this.RaisePropertyChanged(nameof(CanDelete));
			this.RaisePropertyChanged(nameof(CanClone));
		}

		async Task Refresh(Repository update, bool? cloneOrDelete, CancellationToken cancellationToken)
		{
			Icon = "resm:Tgstation.Server.ControlPanel.Assets.hourglass.png";
			Refreshing = true;
			Error = false;
			ErrorMessage = null;
			CloneAvailable = false;
			RecheckCommands();
			try
			{
				var oldRepo = Repository;
				Repository newRepo;
				try
				{
					if (cloneOrDelete.HasValue)
						if (cloneOrDelete.Value)
							newRepo = await repositoryClient.Clone(update, cancellationToken).ConfigureAwait(true);
						else
							newRepo = await repositoryClient.Delete(cancellationToken).ConfigureAwait(true);
					else if (update != null)
						newRepo = await repositoryClient.Update(update, cancellationToken).ConfigureAwait(true);
					else
						newRepo = await repositoryClient.Read(cancellationToken).ConfigureAwait(true);

					if (newRepo.ActiveJob != null)
						jobSink.RegisterJob(newRepo.ActiveJob);
					if (newRepo.Reference == null)
						newRepo.Reference = "(unknown)";
					if (newRepo.AccessUser == null)
						newRepo.AccessUser = NoCredentials;


					Repository = newRepo;

					NewOrigin = String.Empty;
					NewSha = String.Empty;
					NewReference = String.Empty;
					NewCommitterEmail = String.Empty;
					NewCommitterName = String.Empty;
					NewAccessUser = String.Empty;
					NewAccessToken = String.Empty;

					NewUpdateFromOrigin = false;
					NewAutoUpdatesKeepTestMerges = Repository.AutoUpdatesKeepTestMerges ?? update.AutoUpdatesKeepTestMerges ?? oldRepo.AutoUpdatesKeepTestMerges ?? NewAutoUpdatesKeepTestMerges;
					NewAutoUpdatesSynchronize = Repository.AutoUpdatesSynchronize ?? update.AutoUpdatesSynchronize ?? oldRepo.AutoUpdatesSynchronize ?? NewAutoUpdatesSynchronize;
					NewShowTestMergeCommitters = Repository.ShowTestMergeCommitters ?? update.ShowTestMergeCommitters ?? oldRepo.ShowTestMergeCommitters ?? NewShowTestMergeCommitters;

					CloneAvailable = Repository.Origin == null;
				}
				catch (ClientException e)
				{
					Error = true;
					ErrorMessage = e.Message;
				}

				//TODO: Pull requests

				var client = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("test", "1.0.0")) {
					Credentials = new Octokit.Credentials("d66de08a0f1c70b3c9a463ef8abe8bee739cf7c9")
				};
				var prs = await client.Search.SearchIssues(new Octokit.SearchIssuesRequest
				{
					Repos = new Octokit.RepositoryCollection { { "tgstation", "tgstation" } },
					State = Octokit.ItemState.Open,
					Type = Octokit.IssueTypeQualifier.PullRequest,
				}).ConfigureAwait(true);

				var tasks = prs.Items.Select(x => client.PullRequest.Commits("tgstation", "tgstation", x.Number));
				await Task.WhenAll(tasks).ConfigureAwait(true);

				RemotePullRequests = Enumerable.Zip(prs.Items, tasks.Select(x => x.Result), (a, b) => new TestMergeViewModel(a, b, x => Task.CompletedTask)).ToList();
			}
			finally
			{
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.git.png";
				Refreshing = false;
				RecheckCommands();
			}
		}

		public Task HandleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}

		public bool CanRunCommand(RepositoryCommand command)
		{
			switch (command)
			{
				case RepositoryCommand.Close:
					return true;
				case RepositoryCommand.Refresh:
					return !Refreshing && rightsProvider.RepositoryRights.HasFlag(RepositoryRights.Read);
				case RepositoryCommand.Update:
					return !Refreshing
						&& (CanAutoUpdate | CanChangeCommitter | CanAccess | CanShowTMCommitters | CanTestMerge | CanSetRef | CanSetSha | CanUpdate)
						&& !(!String.IsNullOrEmpty(NewAccessUser) ^ !String.IsNullOrEmpty(NewAccessToken));
				case RepositoryCommand.Delete:
					return CanDelete;
				case RepositoryCommand.Clone:
					return CanClone && !String.IsNullOrEmpty(NewOrigin) && !(!String.IsNullOrEmpty(NewAccessUser) ^ !String.IsNullOrEmpty(NewAccessToken));
				case RepositoryCommand.RemoveCredentials:
					return CanAccess;
				case RepositoryCommand.DirectAddPR:
				case RepositoryCommand.RefreshPRs:
					return CanTestMerge;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}

		public async Task RunCommand(RepositoryCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case RepositoryCommand.Close:
					pageContext.ActiveObject = null;
					break;
				case RepositoryCommand.Refresh:
					await Refresh(null, null, cancellationToken).ConfigureAwait(true);
					break;
				case RepositoryCommand.Update:
					var update = new Repository
					{
						CheckoutSha = String.IsNullOrEmpty(NewSha) || !CanSetSha ? null : NewSha,
						Reference = String.IsNullOrEmpty(NewReference) || !CanSetRef ? null : NewReference,
						UpdateFromOrigin = CanUpdate ? (bool?)NewUpdateFromOrigin : null,

						AccessToken = String.IsNullOrEmpty(NewAccessToken) || !CanAccess ? null : NewAccessToken,
						AccessUser = String.IsNullOrEmpty(NewAccessUser) || !CanAccess ? null : NewAccessUser,

						AutoUpdatesKeepTestMerges = CanAutoUpdate ? (bool?)NewAutoUpdatesKeepTestMerges : null,
						AutoUpdatesSynchronize = CanAutoUpdate ? (bool?)NewAutoUpdatesSynchronize : null,

						ShowTestMergeCommitters = CanShowTMCommitters ? (bool?)NewShowTestMergeCommitters : null,

						CommitterEmail = CanChangeCommitter ? NewCommitterEmail : null,
						CommitterName = CanChangeCommitter ? NewCommitterName : null
					};
					await Refresh(update, null, cancellationToken).ConfigureAwait(true);
					if (DeployAfter)
					{
						var job = await dreamMakerClient.Compile(cancellationToken).ConfigureAwait(true);
						jobSink.RegisterJob(job);
					}
					break;
				case RepositoryCommand.Delete:
					await Refresh(null, false, cancellationToken).ConfigureAwait(true);
					break;
				case RepositoryCommand.Clone:
					var clone = new Repository
					{
						Origin = NewOrigin,
						Reference = String.IsNullOrEmpty(NewReference) ? null : NewReference,
						AccessToken = String.IsNullOrEmpty(NewAccessToken) || !CanAccess ? null : NewAccessToken,
						AccessUser = String.IsNullOrEmpty(NewAccessUser) || !CanAccess ? null : NewAccessUser,
					};
					await Refresh(clone, true, cancellationToken).ConfigureAwait(true);
					break;
				case RepositoryCommand.RemoveCredentials:
					await Refresh(new Repository
					{
						AccessUser = String.Empty
					}, null, cancellationToken).ConfigureAwait(true);
					break;
				case RepositoryCommand.DirectAddPR:
					throw new NotImplementedException();
				case RepositoryCommand.RefreshPRs:
					throw new NotImplementedException();
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
	}
}
