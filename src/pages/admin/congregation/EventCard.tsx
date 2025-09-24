// EventCard.tsx
import React from 'react';
import { Users, Clock, MapPin } from 'lucide-react';

interface Duty {
  role: string;
  person: string;
  position: string;
  branch: string;
}

export interface Event {
  id: number;
  title: string;
  members: number;
  created: string;
  startDate: string;
  startTime: string;
  duties: Duty[];
  location: string;
  description: string;
  assignedBranches: string[];
  agenda: string;
  venue: string;
  dates: Date[];
  branch: string;
  service: string;
}

interface EventCardProps {
  event: Event;
  onClick: (event: Event) => void;
  index: number;
}

const EventCard: React.FC<EventCardProps> = ({ event, onClick, index }) => {
  const handleClick = () => {
    onClick(event);
  };

  return (
    <div
      className="p-2 rounded-lg transition-all duration-500 hover:scale-[1.02] animate-fadeIn
                relative overflow-hidden group cursor-pointer border border-green-200 bg-white"
      style={{
        animationDelay: `${index * 0.1}s`,
        transition: 'transform 0.5s ease-out, opacity 0.5s ease-out'
      }}
      onClick={handleClick}
    >
      {/* Content */}
      <div className="relative z-10">
        <div className="flex justify-between items-start mb-1">
          <span className="text-gray-800 text-xs font-medium truncate">{event.title}</span>
        </div>
        <div className="flex items-center gap-3 text-gray-600 text-[0.6rem]">
          <div className="flex items-center gap-1">
            <MapPin className="h-3 w-3" />
            <span>{event.venue}</span>
          </div>
          <div className="flex items-center gap-1">
            <Users className="h-3 w-3" />
            <span>{event.members} attending</span>
          </div>
        </div>
        <div className="flex items-center gap-3 text-gray-600 text-[0.6rem] mt-1">
          <div className="flex items-center gap-1">
            <Clock className="h-3 w-3" />
            <span>{event.startDate} at {event.startTime}</span>
          </div>
        </div>
        <div className="text-gray-500 text-[0.55rem] mt-1">
          Created {event.created}
        </div>
      </div>
    </div>
  );
};

export default EventCard;
