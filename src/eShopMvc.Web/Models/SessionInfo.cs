namespace eShopMvc.Web.Models;

/// <summary>The footer's session values (Decisions R-11, P2-8); the layout printed "{MachineName}, {SessionStartTime}".</summary>
public sealed record SessionInfo(string MachineName, string SessionStartTime);
