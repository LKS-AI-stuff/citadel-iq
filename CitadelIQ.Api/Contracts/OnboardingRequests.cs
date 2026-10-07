namespace CitadelIQ.Api.Contracts;

public record CreateOrganizationRequest(string Name);

public record JoinOrganizationRequest(string JoinCode);
