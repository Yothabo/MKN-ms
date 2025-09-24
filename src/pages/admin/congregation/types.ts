export interface Duty {
  role: string;
  person: string;
  position: string;
  branch: string;
}

export interface Event {
  id: number;
  title: string;
  members: number;
  rsvped: number;
  created: string;
  endsIn: string;
  isLive: boolean;
  agenda: string;
  zoomLink: string;
  startTime: string;
  startDate: string;
  duties: Duty[];
  location: string;
  description: string;
  assignedBranches: string[];
}

export interface Card {
  id: number;
  date: string;
  title: string;
  description: string;
  type: string;
}
