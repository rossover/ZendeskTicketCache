using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using ZendeskApi_v2;
using FastMember;

static class Main
{
	public const string API_ENDPOINT = "https://yourcompany.zendesk.com/api/v2";
	public const string API_USERNAME = "email@yourcompany.com";

	public const string API_TOKEN = "abcd1234";

	public const long YOURCOMPANY_ORGANIZATION_ID = 29958914;
	public const long TIME_SPENT_CUSTOM_FIELD_ID = 22682894;
	public const long CATEGORY_CUSTOM_FIELD_ID = 30240257;
	public const long ACTION_CUSTOM_FIELD_ID = 22397090;

	public const long CLOSE_CODE_CUSTOM_FIELD_ID = 30242258;

	public const string SQL_CONNECTION_STRING = "";
	public static int Main(string[] args)
	{
		ZendeskTicketCache ZendeskTicketCache = new ZendeskTicketCache(SQL_CONNECTION_STRING);
		ZendeskTicketCache.UpdateTickets((args != null) && args.Count == 1 ? args(0) : string.Empty);
		if (args == null || args.Count == 0)
			ZendeskTicketCache.RemoveDeletedTickets();
		Console.WriteLine("Done");
		return 0;
		//success
	}

	private class ZendeskTicketCache
	{
		private ZendeskApi API = new ZendeskApi(Main.API_ENDPOINT, Main.API_USERNAME, string.Empty, Main.API_TOKEN);
		private string CONN_STRING;
		private Dictionary<long, DateTime> TICKET_CACHE = new Dictionary<long, DateTime>();

		private List<long> TICKET_CACHE_NEW_OPEN = new List<long>();
		public ZendeskTicketCache(string aConnString)
		{
			this.CONN_STRING = aConnString;
		}

		public class TicketWithMoreInfo
		{
			public Ticket Ticket = new Ticket();
			public List<Timelog> Timelog = new List<Timelog>();
			public List<AssigneeChange> AssigneeChange = new List<AssigneeChange>();
			public List<Comment> Comment = new List<Comment>();
			public List<GroupChange> GroupChange = new List<GroupChange>();
			public List<StatusChange> StatusChange = new List<StatusChange>();
		}

		private string GetSearchQuery(System.DateTime ModifiedDate)
		{
			return "updated>=" + ModifiedDate.AddDays(0).ToString("yyyy-MM-dd");
		}

		private System.DateTime GetLastModifedDate()
		{
			DateTime myDate = default(DateTime);
			using (SqlClient.SqlConnection myConnection = new SqlClient.SqlConnection(this.CONN_STRING)) {
				myConnection.Open();
				using (SqlClient.SqlCommand myCommand = new SqlClient.SqlCommand("SELECT MAX(Modified_Date) as LastDate FROM tblZendeskTicket WITH(NOLOCK)", myConnection)) {
					object myResult = myCommand.ExecuteScalar;
					myDate = myResult == null || object.ReferenceEquals(myResult, DBNull.Value) ? System.DateTime.Today.AddDays(-45) : (DateTime)myCommand.ExecuteScalar;
				}
				myConnection.Close();
			}
			return myDate;
		}

		public void RemoveDeletedTickets()
		{
			List<long> myH2O = new List<long>();
			using (SqlClient.SqlConnection myConnection = new SqlClient.SqlConnection(this.CONN_STRING)) {
				myConnection.Open();
				using (SqlClient.SqlCommand myCommand = new SqlClient.SqlCommand("SELECT Ticket_ID FROM tblZendeskTicket WITH(NOLOCK) WHERE Status IN ('NEW','OPEN')", myConnection)) {
					SqlClient.SqlDataReader myReader = myCommand.ExecuteReader();
					while (myReader.Read) {
						myH2O.Add(myReader.GetInt64(0));
					}
				}
				myConnection.Close();
			}

			List<long> myZD = new List<long>();
			Models.Search.SearchResults mySearchResults = API.Search.SearchFor("type:ticket status<pending");
			while (mySearchResults != null && mySearchResults.Results != null && mySearchResults.Results.Count > 0) {
				foreach (Models.Search.Result myItem in mySearchResults.Results) {
					if (!myZD.Contains(myItem.Id))
						myZD.Add(myItem.Id);
				}
				mySearchResults = mySearchResults.Count > mySearchResults.Results.Count ? API.Search.GetByPageUrl<Models.Search.SearchResults>(mySearchResults.NextPage) : null;
			}

			List<long> myListToDelete = new List<long>();
			foreach (long myID in myH2O) {
				if (!myZD.Contains(myID)) {
					if (!DoesTicketExist(myID))
						myListToDelete.Add(myID);
				}
			}

			if (myListToDelete.Count > 0) {
				using (SqlClient.SqlConnection myConnection = new SqlClient.SqlConnection(this.CONN_STRING)) {
					myConnection.Open();
					using (SqlClient.SqlCommand myCommand = new SqlClient.SqlCommand(string.Format("DELETE FROM tblZendeskTicket WHERE Ticket_ID IN ({0})", string.Join(",", myListToDelete)), myConnection)) {
						myCommand.ExecuteNonQuery();
					}
					myConnection.Close();
				}
			}

			Console.WriteLine("Done checking for deleted tickets.");
		}

		private void LoadCacheWithLastUpdated()
		{
			this.TICKET_CACHE_NEW_OPEN = new List<long>();
			using (SqlClient.SqlConnection myConnection = new SqlClient.SqlConnection(this.CONN_STRING)) {
				myConnection.Open();
				using (SqlClient.SqlCommand myCommand = new SqlClient.SqlCommand("SELECT Ticket_ID, Modified_Date FROM tblZendeskTicket WITH(NOLOCK)", myConnection)) {
					SqlClient.SqlDataReader myReader = myCommand.ExecuteReader();
					while (myReader.Read) {
						this.TICKET_CACHE.Add(myReader.GetInt64(0), myReader.GetDateTime(1));
					}
				}
				myConnection.Close();
			}
		}

		private DateTime ConvertDateTime(DateTimeOffset aDateTime)
		{
			return TimeZoneInfo.ConvertTimeFromUtc(aDateTime.ToUniversalTime.DateTime, TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"));
		}

		private bool DoesTicketExist(long TicketId)
		{
			string mySearchQuery = "type:ticket " + TicketId.ToString;
			Console.WriteLine("Checking for existence of ticket: " + TicketId.ToString + "");

			Models.Search.SearchResults mySearchResults = API.Search.SearchFor(mySearchQuery);
			foreach (Models.Search.Result myResult in mySearchResults.Results) {
				if (myResult.Id == TicketId)
					return true;
			}

			Console.WriteLine("Ticket " + TicketId.ToString + " not found.");
			return false;
		}

		public List<Ticket> UpdateTickets(string aQuery = "")
		{
			Console.WriteLine("Loading cache...");
			this.LoadCacheWithLastUpdated();
			Console.WriteLine("Finished loading cache.");

			Dictionary<long, Ticket> myTickets = new Dictionary<long, Ticket>();
			List<Timelog> myTimelogs = new List<Timelog>();
			List<Comment> myComments = new List<Comment>();
			List<GroupChange> myGroupChanges = new List<GroupChange>();
			List<StatusChange> myStatusChanges = new List<StatusChange>();
			List<AssigneeChange> myAssigneeChanges = new List<AssigneeChange>();

			string[] mySearchQueries = null;
			if ((aQuery != null) && !string.IsNullOrEmpty(aQuery)) {
				mySearchQueries = new string[1];
				mySearchQueries(0) = aQuery;
			} else {
				mySearchQueries = {
					GetSearchQuery(GetLastModifedDate()),
					"status<closed"
				};
			}

			foreach (void mySearchQuery_loopVariable in mySearchQueries) {
				mySearchQuery = mySearchQuery_loopVariable;
				mySearchQuery = "type:ticket " + mySearchQuery;
				Console.WriteLine("Starting query:  " + mySearchQuery + "");

				Models.Search.SearchResults mySearchResults = API.Search.SearchFor(mySearchQuery);
				long myCounter = 0;
				while (mySearchResults != null && mySearchResults.Results != null && mySearchResults.Results.Count > 0) {
					Console.WriteLine("Search Results: " + mySearchResults.Count.ToString + " (" + myCounter.ToString + " so far)");
					foreach (Models.Search.Result myItem in mySearchResults.Results) {
						myCounter += 1;
						if (!myTickets.ContainsKey(myItem.Id)) {
							System.DateTime.Today.ToUniversalTime();
							if (TICKET_CACHE.ContainsKey(myItem.Id) && TICKET_CACHE(myItem.Id) == ConvertDateTime(myItem.UpdatedAt.Value)) {
								Console.WriteLine("No update needed for " + myItem.Id.ToString);
							} else {
								TicketWithMoreInfo myLookup = LookupTicket(myItem);
								myTickets.Add(myLookup.Ticket.Ticket_ID, myLookup.Ticket);
								myTimelogs.AddRange(myLookup.Timelog);
								myComments.AddRange(myLookup.Comment);
								myGroupChanges.AddRange(myLookup.GroupChange);
								myStatusChanges.AddRange(myLookup.StatusChange);
								myAssigneeChanges.AddRange(myLookup.AssigneeChange);
							}
						}
					}

					if (myTickets.Any && myTickets.Count >= 250) {
						Console.WriteLine("Flushing to database...");
						WriteContentToDb(myTickets.Values.ToList(), myTimelogs, myComments, myGroupChanges, myAssigneeChanges, myStatusChanges);
						myTickets.Clear();
						myAssigneeChanges.Clear();
						myTimelogs.Clear();
						myComments.Clear();
						myGroupChanges.Clear();
						myStatusChanges.Clear();
						Console.WriteLine("Finished flushing to database.");
					}

					mySearchResults = mySearchResults.Count > mySearchResults.Results.Count ? API.Search.GetByPageUrl<Models.Search.SearchResults>(mySearchResults.NextPage) : null;
				}
			}

			if (myTickets.Any) {
				WriteContentToDb(myTickets.Values.ToList(), myTimelogs, myComments, myGroupChanges, myAssigneeChanges, myStatusChanges);
			} else {
				Console.WriteLine("No updates to database needed.");
			}
			return myTickets.Values.ToList();
		}


		private void WriteContentToDb(List<Ticket> myTickets, List<Timelog> myTimelogs, List<Comment> myComments, List<GroupChange> myGroupChanges, List<AssigneeChange> myAssigneeChanges, List<StatusChange> myStatusChanges)
		{
			using (SqlClient.SqlConnection myConnection = new SqlClient.SqlConnection(CONN_STRING)) {
				myConnection.Open();

				using (SqlClient.SqlCommand myCommand = new SqlClient.SqlCommand("create table #temp (Ticket_ID INT)", myConnection)) {
					myCommand.ExecuteNonQuery();
				}

				BulkInsertlongs(myConnection, "#temp", myTickets.Select<long>(t => t.Ticket_ID).ToArray.ToList, "Ticket_ID");
				using (SqlClient.SqlCommand myCommand = new SqlClient.SqlCommand("DELETE FROM tblZendeskticket WHERE Ticket_ID IN (SELECT Ticket_ID FROM #temp)", myConnection)) {
					myCommand.ExecuteNonQuery();
				}

				BulkInsert<Ticket>(myConnection, "tblZendeskTicket", myTickets);
				BulkInsert<Timelog>(myConnection, "tblZendeskTimelog", myTimelogs);
				BulkInsert<AssigneeChange>(myConnection, "tblZendeskAssigneeChange", myAssigneeChanges);
				BulkInsert<Comment>(myConnection, "tblZendeskComment", myComments);
				BulkInsert<StatusChange>(myConnection, "tblZendeskStatusChange", myStatusChanges);
				BulkInsert<GroupChange>(myConnection, "tblZendeskGroupChange", myGroupChanges);

				myConnection.Close();
			}
		}

		private void BulkInsertlongs(SqlClient.SqlConnection aConnection, string TableName, List<long> ListOfStuff, string FieldName)
		{
			DataTable myDT = new DataTable();
			myDT.Columns.Add(new DataColumn(FieldName, typeof(long)));
			foreach (long a in ListOfStuff) {
				myDT.Rows.Add({ a });
			}
			using (SqlClient.SqlBulkCopy myBulk = new SqlClient.SqlBulkCopy(aConnection)) {
				foreach (DataColumn myColumn in myDT.Columns) {
					myBulk.ColumnMappings.Add(new SqlClient.SqlBulkCopyColumnMapping(myColumn.ColumnName, myColumn.ColumnName));
				}
				myBulk.DestinationTableName = TableName;
				myBulk.WriteToServer(myDT);
			}
		}

		private void BulkInsert<T>(SqlClient.SqlConnection aConnection, string TableName, List<T> ListOfStuff)
		{
			DataTable myDT = new DataTable();
			using (ObjectReader myReader = ObjectReader.Create<T>(ListOfStuff)) {
				myDT.Load(myReader);
			}

			using (SqlClient.SqlBulkCopy myBulk = new SqlClient.SqlBulkCopy(aConnection)) {
				foreach (DataColumn myColumn in myDT.Columns) {
					myBulk.ColumnMappings.Add(new SqlClient.SqlBulkCopyColumnMapping(myColumn.ColumnName, myColumn.ColumnName));
				}
				myBulk.DestinationTableName = TableName;
				myBulk.WriteToServer(myDT);
			}
		}

		private List<Models.Shared.Audit> GetAudits(long TicketID)
		{
			List<Models.Shared.Audit> myAudits = new List<Models.Shared.Audit>();

			ZendeskApi_v2.Models.Shared.GroupAuditResponse myAuditGroup = API.Tickets.GetAudits(TicketID);
			do {
				if ((myAuditGroup.Audits != null)) {
					myAudits.AddRange(myAuditGroup.Audits);
					myAuditGroup = myAuditGroup.NextPage == null ? null : API.Search.GetByPageUrl<Models.Shared.GroupAuditResponse>(myAuditGroup.NextPage);
				}
			} while (myAuditGroup != null);

			return myAudits;
		}

		public TicketWithMoreInfo LookupTicket(Models.Search.Result aResult)
		{
			Console.WriteLine("Getting " + aResult.Id.ToString);

			TicketWithMoreInfo myReturn = new TicketWithMoreInfo();
			var _with1 = myReturn.Ticket;
			_with1.Subject = aResult.Subject;
			_with1.Assignee = LookupUser(aResult.AssigneeId.GetValueOrDefault).Name;
			_with1.Organization = LookupOrganization(aResult.OrganizationId.GetValueOrDefault).Name;
			_with1.Ticket_ID = aResult.Id;
			_with1.Priority = aResult.Priority;
			_with1.Created_Date = ConvertDateTime(aResult.CreatedAt.Value);
			_with1.Tags = aResult.Tags != null ? string.Join(" ", aResult.Tags) : string.Empty;
			_with1.Group = LookupGroup(aResult.GroupId.GetValueOrDefault).Name;
			_with1.Modified_Date = ConvertDateTime(aResult.UpdatedAt.Value);
			_with1.Status = aResult.Status;
			_with1.Category = aResult.CustomFields.FirstOrDefault(a => a.Id == Main.CATEGORY_CUSTOM_FIELD_ID).Value ?? "";
			_with1.Action = aResult.CustomFields.FirstOrDefault(a => a.Id == Main.ACTION_CUSTOM_FIELD_ID).Value ?? "";
			_with1.Close_Code = aResult.CustomFields.FirstOrDefault(a => a.Id == Main.CLOSE_CODE_CUSTOM_FIELD_ID).Value ?? "";

			List<Models.Shared.Audit> myAudits = GetAudits(aResult.Id);

			foreach (Models.Shared.Audit myAudit in myAudits) {
				Models.Shared.Event mySatisfactionScore = myAudit.Events.Where(a => a.FieldName != null && a.FieldName.ToUpper == "SATISFACTION_SCORE").FirstOrDefault;
				Models.Shared.Event mySatisfactionComment = myAudit.Events.Where(a => a.FieldName != null && a.FieldName.ToUpper == "SATISFACTION_COMMENT").FirstOrDefault;
				if ((mySatisfactionScore != null)) {
					switch (mySatisfactionScore.Value.ToString.ToUpper) {
						case "OFFERED":
						case "UNOFFERED":
						case "":
							myReturn.Ticket.Satisfaction_Comments = string.Empty;
							myReturn.Ticket.Satisfaction_Date = null;
							myReturn.Ticket.Satisfaction_Rating = string.Empty;
							break;
						default:
							myReturn.Ticket.Satisfaction_Comments = mySatisfactionComment == null ? string.Empty : mySatisfactionComment.Value.ToString;
							if (myReturn.Ticket.Satisfaction_Comments.Length > 500)
								myReturn.Ticket.Satisfaction_Comments.Substring(0, 500);
							myReturn.Ticket.Satisfaction_Rating = mySatisfactionScore.Value.ToString;
							myReturn.Ticket.Satisfaction_Date = ConvertDateTime(myAudit.CreatedAt.Value);
							break;
					}
				}

				Models.Shared.Event myTimeEvent = myAudit.Events.Where(a => a.FieldName == Main.TIME_SPENT_CUSTOM_FIELD_ID.ToString).FirstOrDefault;
				if ((myTimeEvent != null)) {
					myReturn.Timelog.Add(new Timelog {
						Duration = long.Parse(myTimeEvent.Value.ToString),
						Created_Date = ConvertDateTime(myAudit.CreatedAt.Value),
						Zendesk_Identifier = myAudit.Id.ToString,
						Ticket_ID = aResult.Id,
						User = LookupUser(myAudit.AuthorId).Name
					});
				}

				Models.Shared.Event myComment = myAudit.Events.Where(a => a.Type.ToUpper == "COMMENT").FirstOrDefault;
				if ((myComment != null)) {
					Models.Users.User myUser = LookupUser(myComment.AuthorId.GetValueOrDefault);
					myReturn.Comment.Add(new Comment {
						Created_Date = ConvertDateTime(myAudit.CreatedAt.Value),
						Zendesk_Identifier = myAudit.Id.ToString,
						Ticket_ID = aResult.Id,
						Type = myComment.Public ? myUser.OrganizationId.GetValueOrDefault == Main.YOURCOMPANY_ORGANIZATION_ID ? Enum_Comment_Type.PUBLIC : Enum_Comment_Type.CUSTOMER : Enum_Comment_Type.INTERNAL.ToString,
						User = myUser.Name
					});
				}

				Models.Shared.Event myGroupChange = myAudit.Events.Where(a => a.Type.ToUpper == "CHANGE" & (a.FieldName != null && a.FieldName.ToUpper == "GROUP_ID")).FirstOrDefault;
				if ((myGroupChange != null)) {
					Models.Users.User myUser = LookupUser(myAudit.AuthorId);
					if (myGroupChange.PreviousValue == null)
						myGroupChange.PreviousValue = "0";
					if (myGroupChange.Value == null)
						myGroupChange.Value = "0";
					Models.Groups.Group myFromGroup = LookupGroup(long.Parse(myGroupChange.PreviousValue.ToString));
					Models.Groups.Group myToGroup = LookupGroup(long.Parse(myGroupChange.Value.ToString));
					myReturn.GroupChange.Add(new GroupChange {
						Created_Date = ConvertDateTime(myAudit.CreatedAt.Value),
						Zendesk_Identifier = myAudit.Id.ToString,
						Ticket_ID = aResult.Id,
						Old_Group = myFromGroup == null ? "" : myFromGroup.Name,
						New_Group = myToGroup == null ? "" : myToGroup.Name,
						By_Customer = !(myUser.OrganizationId.GetValueOrDefault == 0 | myUser.OrganizationId.GetValueOrDefault == Main.YOURCOMPANY_ORGANIZATION_ID),
						User = myUser.Name
					});
				}

				Models.Shared.Event myAssigneeChange = myAudit.Events.Where(a => a.Type.ToUpper == "CHANGE" & (a.FieldName != null && a.FieldName.ToUpper == "ASSIGNEE_ID")).FirstOrDefault;
				if ((myAssigneeChange != null)) {
					Models.Users.User myUser = LookupUser(myAudit.AuthorId);
					if (myAssigneeChange.PreviousValue == null)
						myAssigneeChange.PreviousValue = "0";
					if (myAssigneeChange.Value == null)
						myAssigneeChange.Value = "0";
					Models.Users.User myFromAssignee = LookupUser(long.Parse(myAssigneeChange.PreviousValue.ToString));
					Models.Users.User myToAssignee = LookupUser(long.Parse(myAssigneeChange.Value.ToString));
					myReturn.AssigneeChange.Add(new AssigneeChange {
						Created_Date = ConvertDateTime(myAudit.CreatedAt.Value),
						Zendesk_Identifier = myAudit.Id.ToString,
						Ticket_ID = aResult.Id,
						Old_Assignee = myFromAssignee == null ? "" : myFromAssignee.Name,
						New_Assignee = myToAssignee == null ? "" : myToAssignee.Name,
						User = myUser.Name
					});
				}

				Models.Shared.Event myStatusChange = myAudit.Events.Where(a => a.Type.ToUpper == "CHANGE" & (a.FieldName != null && a.FieldName.ToUpper == "STATUS")).FirstOrDefault;
				if ((myStatusChange != null)) {
					if (myStatusChange.PreviousValue == null)
						myStatusChange.PreviousValue = "";
					if (myStatusChange.Value == null)
						myStatusChange.Value = "";
					Models.Users.User myUser = LookupUser(myAudit.AuthorId);
					myReturn.StatusChange.Add(new StatusChange {
						Created_Date = ConvertDateTime(myAudit.CreatedAt.Value),
						Zendesk_Identifier = myAudit.Id.ToString,
						Ticket_ID = aResult.Id,
						Old_Status = myStatusChange.PreviousValue.ToString,
						New_Status = myStatusChange.Value.ToString,
						By_Customer = !(myUser.OrganizationId.GetValueOrDefault == 0 | myUser.OrganizationId.GetValueOrDefault == Main.YOURCOMPANY_ORGANIZATION_ID),
						User = myUser.Name
					});
				}
			}
			return myReturn;
		}

		private Dictionary<long, Models.Groups.Group> CacheGroup = new Dictionary<long, Models.Groups.Group>();
		private Dictionary<long, Models.Users.User> CacheUser = new Dictionary<long, Models.Users.User>();

		private Dictionary<long, Models.Organizations.Organization> CacheOrganization = new Dictionary<long, Models.Organizations.Organization>();
		private Models.Users.User LookupUser(long UserId)
		{
			if (UserId <= 0)
				return new Models.Users.User {
					Id = UserId,
					Name = ""
				};
			if (!CacheUser.ContainsKey(UserId)) {
				Models.Users.User myUser = null;
				try {
					myUser = API.Users.GetUser(UserId).User;
				} catch (Exception ex) {
					myUser = new Models.Users.User {
						Id = UserId,
						Name = "Unknown"
					};
				}
				CacheUser.Add(UserId, myUser);
			}
			return CacheUser(UserId);
		}

		private Models.Groups.Group LookupGroup(long GroupId)
		{
			if (GroupId <= 0)
				return new Models.Groups.Group {
					Id = GroupId,
					Name = ""
				};
			if (!CacheGroup.ContainsKey(GroupId)) {
				Models.Groups.Group myGroup = null;
				try {
					myGroup = API.Groups.GetGroupById(GroupId).Group;
				} catch (Exception ex) {
					myGroup = new Models.Groups.Group {
						Id = GroupId,
						Name = "Unknown"
					};
				}
				CacheGroup.Add(GroupId, myGroup);
			}
			return CacheGroup(GroupId);
		}

		private Models.Organizations.Organization LookupOrganization(long OrganizationId)
		{
			if (OrganizationId <= 0)
				return new Models.Organizations.Organization {
					Id = OrganizationId,
					Name = ""
				};
			if (!CacheOrganization.ContainsKey(OrganizationId)) {
				Models.Organizations.Organization myOrganization = null;
				try {
					myOrganization = API.Organizations.GetOrganization(OrganizationId).Organization;
				} catch (Exception ex) {
					myOrganization = new Models.Organizations.Organization {
						Id = OrganizationId,
						Name = "Unknown"
					};
				}
				CacheOrganization.Add(OrganizationId, myOrganization);
			}
			return CacheOrganization(OrganizationId);
		}

		public class Ticket
		{
			public long Ticket_ID { get; set; }
			public string Organization { get; set; }
			public string Subject { get; set; }
			public DateTime Created_Date { get; set; }
			public string Tags { get; set; }
			public string Category { get; set; }
			public string Priority { get; set; }
			public string Assignee { get; set; }
			public string Group { get; set; }
			public string Status { get; set; }
			public string Satisfaction_Rating { get; set; }
			public string Satisfaction_Comments { get; set; }
			public System.DateTime? Satisfaction_Date { get; set; }
			public DateTime Modified_Date { get; set; }
			public DateTime Inserted_Date { get; set; }
			public string Action { get; set; }
			public string Close_Code { get; set; }
		}

		public class AssigneeChange
		{
			public long Ticket_ID { get; set; }
			public string Zendesk_Identifier { get; set; }
			public string User { get; set; }
			public string Old_Assignee { get; set; }
			public string New_Assignee { get; set; }
			public DateTime Created_Date { get; set; }
		}

		public class GroupChange
		{
			public long Ticket_ID { get; set; }
			public string Zendesk_Identifier { get; set; }
			public string User { get; set; }
			public string Old_Group { get; set; }
			public string New_Group { get; set; }
			public DateTime Created_Date { get; set; }
			public bool By_Customer { get; set; }
		}

		public class StatusChange
		{
			public long Ticket_ID { get; set; }
			public string Zendesk_Identifier { get; set; }
			public string User { get; set; }
			public string Old_Status { get; set; }
			public string New_Status { get; set; }
			public DateTime Created_Date { get; set; }
			public bool By_Customer { get; set; }
		}

		public class Timelog
		{
			public long Ticket_ID { get; set; }
			public string Zendesk_Identifier { get; set; }
			public string User { get; set; }
			public long Duration { get; set; }
			public DateTime Created_Date { get; set; }
		}

		public class Comment
		{
			public long Ticket_ID { get; set; }
			public string Zendesk_Identifier { get; set; }
			public string User { get; set; }
			public string Type { get; set; }
			public DateTime Created_Date { get; set; }
		}

		public enum Enum_Comment_Type
		{
			CUSTOMER,
			INTERNAL,
			PUBLIC
		}
	}
}
