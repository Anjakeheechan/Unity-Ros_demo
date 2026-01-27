using System;

namespace Assets.Scripts
{
	public class WebSocketMessage
	{
		public string Type { get; set; } = string.Empty;
		public string Payload { get; set; } = string.Empty;
		public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
		public string SenderId { get; set; } = string.Empty;
		public string SenderType { get; set; } = string.Empty;
		public string ReceiverId { get; set; } = string.Empty;
	}
}