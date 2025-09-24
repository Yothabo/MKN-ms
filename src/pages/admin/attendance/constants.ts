export const BRANCHES = ['Johannesburg', 'Bulawayo', 'Gaborone', 'Harare', 'Ireland'] as const;
export const STATUSES = ['Active', 'Pre-RA', 'RA', 'Inactive'] as const;

export interface Service {
  day: string; // e.g., "Sunday"
  name: string; // e.g., "Morning Service"
  startTime: string; // e.g., "07:00"
  endTime: string; // e.g., "09:00"
  windowStart: string; // 1 hour before start, e.g., "06:00"
  windowEnd: string; // 1 hour after end, e.g., "10:00"
}

export const services: Service[] = [
  {
    day: 'Sunday',
    name: 'Morning Service',
    startTime: '07:00',
    endTime: '09:00',
    windowStart: '06:00',
    windowEnd: '10:00',
  },
  {
    day: 'Sunday',
    name: 'Afternoon Service',
    startTime: '13:00',
    endTime: '14:00',
    windowStart: '12:00',
    windowEnd: '15:00',
  },
  {
    day: 'Monday',
    name: 'Evening Service',
    startTime: '18:00',
    endTime: '18:30',
    windowStart: '17:00',
    windowEnd: '19:30',
  },
  {
    day: 'Tuesday',
    name: 'Evening Service',
    startTime: '18:00',
    endTime: '19:00',
    windowStart: '17:00',
    windowEnd: '20:00',
  },
  {
    day: 'Wednesday',
    name: 'Evening Service (School Holidays)',
    startTime: '18:00',
    endTime: '19:00',
    windowStart: '17:00',
    windowEnd: '20:00',
  },
  {
    day: 'Wednesday',
    name: 'Afternoon Service (School Term)',
    startTime: '13:50',
    endTime: '14:50',
    windowStart: '12:50',
    windowEnd: '15:50',
  },
  {
    day: 'Thursday',
    name: 'Afternoon Service',
    startTime: '13:45',
    endTime: '14:55',
    windowStart: '12:45',
    windowEnd: '15:55',
  },
  {
    day: 'Friday',
    name: 'Evening Service',
    startTime: '18:00',
    endTime: '19:00',
    windowStart: '17:00',
    windowEnd: '20:00',
  },
  {
    day: 'Saturday',
    name: 'Morning Service',
    startTime: '07:00',
    endTime: '07:30',
    windowStart: '06:00',
    windowEnd: '08:30',
  },
  {
    day: 'Saturday',
    name: 'Afternoon Service',
    startTime: '13:45',
    endTime: '14:45',
    windowStart: '12:45',
    windowEnd: '15:45',
  },
];

// Helper to convert time string (e.g., "07:00") to minutes since midnight
export const timeToMinutes = (time: string): number => {
  const [hours, minutes] = time.split(':').map(Number);
  return hours * 60 + minutes;
};

// Helper to get the current day and time in SAST
export const getCurrentDateTime = (): { day: string; minutes: number } => {
  const now = new Date();
  // Adjust for SAST (UTC+2)
  const sastOffset = 2 * 60; // 2 hours in minutes
  const utcMinutes = now.getUTCHours() * 60 + now.getUTCMinutes();
  const sastMinutes = (utcMinutes + sastOffset) % (24 * 60);
  const days = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
  const day = days[now.getUTCDay()];
  return { day, minutes: sastMinutes };
};

// Helper to check if school is in term (simplified, can be enhanced with actual school calendar)
export const isSchoolTerm = (date: Date): boolean => {
  // Placeholder: Assume school term is Jan-Apr, Jun-Sep, Nov
  const month = date.getMonth(); // 0=Jan, 11=Dec
  return month <= 3 || (month >= 5 && month <= 8) || month === 10;
};

// Check if the current time is within a service window
export const isServiceWindowOpen = (
  currentTime: { day: string; minutes: number },
  isSchoolTerm: boolean
): boolean => {
  return services.some(service => {
    if (service.day !== currentTime.day) return false;
    // Handle Wednesday services based on school term
    if (service.day === 'Wednesday' && service.name.includes('School Holidays') && isSchoolTerm) return false;
    if (service.day === 'Wednesday' && service.name.includes('School Term') && !isSchoolTerm) return false;
    const windowStart = timeToMinutes(service.windowStart);
    const windowEnd = timeToMinutes(service.windowEnd);
    return currentTime.minutes >= windowStart && currentTime.minutes <= windowEnd;
  });
};

// Get the next service window
export const getNextServiceWindow = (
  currentTime: { day: string; minutes: number },
  isSchoolTerm: boolean
): string => {
  const days = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
  const currentDayIndex = days.indexOf(currentTime.day);
  let nextService: Service | null = null;
  let daysAhead = 0;

  // Check services for the next 7 days
  for (let i = 0; i <= 7; i++) {
    const checkDayIndex = (currentDayIndex + i) % 7;
    const checkDay = days[checkDayIndex];
    const dayServices = services.filter(
      s =>
        s.day === checkDay &&
        (checkDay !== 'Wednesday' ||
          (s.name.includes('School Holidays') && !isSchoolTerm) ||
          (s.name.includes('School Term') && isSchoolTerm))
    );

    for (const service of dayServices) {
      const windowStart = timeToMinutes(service.windowStart);
      if (i === 0 && currentTime.minutes >= windowStart) continue; // Skip past/current services today
      if (!nextService || windowStart < timeToMinutes(nextService.windowStart)) {
        nextService = service;
        daysAhead = i;
      }
    }
    if (nextService) break;
  }

  if (!nextService) return 'tomorrow at an unknown time';

  const timeDescription = nextService.name.toLowerCase().includes('morning')
    ? 'morning'
    : nextService.name.toLowerCase().includes('afternoon')
    ? 'afternoon'
    : 'evening';
  const dayDescription = daysAhead === 0 ? 'today' : daysAhead === 1 ? 'tomorrow' : `on ${nextService.day}`;
  return `${dayDescription} in the ${timeDescription} at ${nextService.windowStart}`;
};

// Simulate automatic submission at the end of the service window
export const scheduleAutoSubmit = (
  isWindowOpen: boolean,
  currentTime: { day: string; minutes: number },
  isSchoolTerm: boolean,
  submitCallback: () => void
): NodeJS.Timeout | null => {
  if (!isWindowOpen) return null;

  const currentService = services.find(
    s =>
      s.day === currentTime.day &&
      (s.day !== 'Wednesday' ||
        (s.name.includes('School Holidays') && !isSchoolTerm) ||
        (s.name.includes('School Term') && isSchoolTerm)) &&
      currentTime.minutes >= timeToMinutes(s.windowStart) &&
      currentTime.minutes <= timeToMinutes(s.windowEnd)
  );

  if (!currentService) return null;

  const windowEndMinutes = timeToMinutes(currentService.windowEnd);
  const minutesUntilEnd = windowEndMinutes - currentTime.minutes;
  if (minutesUntilEnd <= 0) return null;

  return setTimeout(submitCallback, minutesUntilEnd * 60 * 1000);
};

