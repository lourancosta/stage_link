// Shapes of the JSON the backend sends (camelCase, matching the C# records).

export interface DiscoveredSpeaker {
  id: string;
  name: string;
  model: string;
  firmware: string;
  address: string;
  tcpPort: number;
  lastSeen: string;
}
