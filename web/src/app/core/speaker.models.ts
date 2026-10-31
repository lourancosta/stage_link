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

export interface SpeakerStatus {
  name: string;
  gain: number;
  muted: boolean;
  tilt: number;
}

// The speaker's allowed ranges (same as SpeakerState in the simulator).
export const SPEAKER_LIMITS = {
  gain: { min: -60, max: 12, step: 0.5 },
  tilt: { min: -15, max: 15, step: 0.5 },
} as const;
