namespace Docly.App.Models;

/// <summary>One earlier message passed to the model as conversation history.</summary>
public sealed record ChatTurn(string Role, string Content);
