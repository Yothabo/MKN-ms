import React, { useState, useRef, useEffect, useCallback, Component } from 'react';
import type { ReactNode } from 'react';
import { Bar, Line, Pie } from 'react-chartjs-2';
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  BarElement,
  LineElement,
  PointElement,
  ArcElement,
  Title,
  Tooltip as ChartJSTooltip,
  Legend,
} from 'chart.js';
import { motion } from 'framer-motion';
import { FiUsers, FiActivity, FiTrendingUp, FiAlertTriangle, FiCheckCircle } from 'react-icons/fi';
import { WorldMap } from 'react-svg-worldmap';
import MKNLogo from '../../../assets/MKN.png';

// Register Chart.js components
ChartJS.register(CategoryScale, LinearScale, BarElement, LineElement, PointElement, ArcElement, Title, ChartJSTooltip, Legend);

// Data
const membershipGrowthData = [
  { month: 'Jan', newJoins: 50, drop: 10 },
  { month: 'Feb', newJoins: 60, drop: 12 },
  { month: 'Mar', newJoins: 55, drop: 15 },
  { month: 'Apr', newJoins: 70, drop: 8 },
  { month: 'May', newJoins: 65, drop: 10 },
  { month: 'Jun', newJoins: 80, drop: 5 },
  { month: 'Jul', newJoins: 75, drop: 7 },
  { month: 'Aug', newJoins: 85, drop: 6 },
  { month: 'Sep', newJoins: 70, drop: 9 },
  { month: 'Oct', newJoins: 90, drop: 4 },
  { month: 'Nov', newJoins: 95, drop: 3 },
  { month: 'Dec', newJoins: 100, drop: 2 },
];

const membersByRegionData = [
  { region: 'Africa', members: 500 },
  { region: 'Europe', members: 300 },
  { region: 'Asia', members: 200 },
  { region: 'North America', members: 150 },
  { region: 'South America', members: 100 },
];

const membersByCountryData = [
  { country: 'ZA', value: 414634, branches: 10, name: 'South Africa' },
  { country: 'NG', value: 103659, branches: 3, name: 'Nigeria' },
  { country: 'US', value: 155488, branches: 5, name: 'United States' },
  { country: 'UK', value: 124390, branches: 4, name: 'United Kingdom' },
  { country: 'BR', value: 82927, branches: 2, name: 'Brazil' },
  { country: 'IN', value: 259147, branches: 6, name: 'India' },
  { country: 'AU', value: 93293, branches: 3, name: 'Australia' },
  { country: 'DE', value: 113925, branches: 4, name: 'Germany' },
  { country: 'CA', value: 134829, branches: 4, name: 'Canada' },
  { country: 'JP', value: 72561, branches: 2, name: 'Japan' },
  { country: 'FR', value: 103659, branches: 3, name: 'France' },
  { country: 'MX', value: 62195, branches: 2, name: 'Mexico' },
  { country: 'KE', value: 82927, branches: 2, name: 'Kenya' },
  { country: 'GH', value: 51829, branches: 1, name: 'Ghana' },
];

const branchMembersData = [
  { branch: 'Branch A', active: 100, inactive: 20, preRA: 15, ra: 10, total: 145 },
  { branch: 'Branch B', active: 80, inactive: 15, preRA: 12, ra: 8, total: 115 },
  { branch: 'Branch C', active: 120, inactive: 10, preRA: 18, ra: 12, total: 160 },
  { branch: 'Branch D', active: 90, inactive: 25, preRA: 20, ra: 15, total: 150 },
];

const attendanceTrendData = [
  { date: 'Jan', attendance: 300 },
  { date: 'Feb', attendance: 320 },
  { date: 'Mar', attendance: 310 },
  { date: 'Apr', attendance: 340 },
  { date: 'May', attendance: 350 },
  { date: 'Jun', attendance: 360 },
  { date: 'Jul', attendance: 355 },
  { date: 'Aug', attendance: 370 },
  { date: 'Sep', attendance: 345 },
  { date: 'Oct', attendance: 380 },
  { date: 'Nov', attendance: 390 },
  { date: 'Dec', attendance: 400 },
];

const attendanceByDayData = [
  { day: 'Sun', value: 400 },
  { day: 'Mon', value: 50 },
  { day: 'Tue', value: 60 },
  { day: 'Wed', value: 100 },
  { day: 'Thu', value: 70 },
  { day: 'Fri', value: 80 },
  { day: 'Sat', value: 200 },
];

const attendanceByWeekData = [
  { week: 'Week 1', value: 320 },
  { week: 'Week 2', value: 300 },
  { week: 'Week 3', value: 340 },
  { week: 'Week 4', value: 310 },
];

const branchAttendanceData = [
  { branch: 'Branch A', attendanceRate: 85 },
  { branch: 'Branch B', attendanceRate: 75 },
  { branch: 'Branch C', attendanceRate: 90 },
  { branch: 'Branch D', attendanceRate: 65 },
];

const virginityStatusData = [
  { name: 'Virgin', value: 60 },
  { name: 'Non', value: 25 },
  { name: 'Inapplicable', value: 15 },
];

const virginityTrendData = [
  { year: '2022', virgin: 82, non: 10, inapplicable: 8 },
  { year: '2023', virgin: 78, non: 14, inapplicable: 8 },
  { year: '2024', virgin: 80, non: 11, inapplicable: 9 },
  { year: '2025', virgin: 76, non: 15, inapplicable: 9 },
];

const virginityByAgeData = [
  { ageGroup: '13-18', virgin: 85, non: 10, inapplicable: 5 },
  { ageGroup: '19-23', virgin: 60, non: 25, inapplicable: 15 },
  { ageGroup: '24+', virgin: 50, non: 30, inapplicable: 20 },
];

const virginityByBranchData = [
  { branch: 'Branch A', virgin: 80, non: 15, inapplicable: 5 },
  { branch: 'Branch B', virgin: 70, non: 20, inapplicable: 10 },
  { branch: 'Branch C', virgin: 75, non: 15, inapplicable: 10 },
  { branch: 'Branch D', virgin: 65, non: 25, inapplicable: 10 },
];

const retentionFunnelData = [
  { stage: 'Active', count: 900 },
  { stage: 'Inactive', count: 200 },
  { stage: 'Pre-RA', count: 250 },
  { stage: 'RA', count: 150 },
];

const raPreRATrendData = [
  { month: 'Jan', active: 65, inactive: 10, preRA: 15, ra: 10 },
  { month: 'Feb', active: 64, inactive: 11, preRA: 15, ra: 10 },
  { month: 'Mar', active: 62, inactive: 12, preRA: 16, ra: 10 },
  { month: 'Apr', active: 58, inactive: 15, preRA: 18, ra: 9 },
  { month: 'May', active: 55, inactive: 17, preRA: 19, ra: 9 },
  { month: 'Jun', active: 53, inactive: 19, preRA: 20, ra: 8 },
  { month: 'Jul', active: 54, inactive: 19, preRA: 18, ra: 9 },
  { month: 'Aug', active: 55, inactive: 19, preRA: 17, ra: 9 },
  { month: 'Sep', active: 56, inactive: 19, preRA: 16, ra: 9 },
  { month: 'Oct', active: 54, inactive: 20, preRA: 16, ra: 10 },
  { month: 'Nov', active: 52, inactive: 21, preRA: 16, ra: 11 },
  { month: 'Dec', active: 50, inactive: 22, preRA: 16, ra: 12 },
];

const branchAlertsData = [
  { branch: 'Branch A', raPercentage: 10, preRAPercentage: 15, attendanceRate: 85, inactivePercentage: parseFloat(((20 / 145) * 100).toFixed(2)) },
  { branch: 'Branch B', raPercentage: 15, preRAPercentage: 18, attendanceRate: 75, inactivePercentage: parseFloat(((15 / 115) * 100).toFixed(2)) },
  { branch: 'Branch C', raPercentage: 8, preRAPercentage: 12, attendanceRate: 90, inactivePercentage: parseFloat(((10 / 160) * 100).toFixed(2)) },
  { branch: 'Branch D', raPercentage: 20, preRAPercentage: 25, attendanceRate: 65, inactivePercentage: parseFloat(((25 / 150) * 100).toFixed(2)) },
];

const branchRetentionData = [
  { branch: 'Branch A', retentionRate: parseFloat(((100 / 145) * 100).toFixed(2)) }, // 68.97%
  { branch: 'Branch B', retentionRate: parseFloat(((80 / 115) * 100).toFixed(2)) },  // 69.57%
  { branch: 'Branch C', retentionRate: parseFloat(((120 / 160) * 100).toFixed(2)) }, // 75.00%
  { branch: 'Branch D', retentionRate: parseFloat(((90 / 150) * 100).toFixed(2)) },  // 60.00%
];

const membershipInsightsData = [
  { insight: 'South Africa has the highest membership growth.' },
  { insight: 'Membership drops are lowest in December.' },
  { insight: 'Africa has the most members compared to other regions.' },
  { insight: 'Branch D shows the highest Inactive members, indicating members who passed away or left the religion.' },
];

const attendanceInsightsData = [
  { insight: 'Sunday has the highest attendance.' },
  { insight: 'Branch D has the lowest attendance rate.' },
  { insight: 'October has the highest monthly attendance.' },
];

const purityInsightsData = [
  { insight: '19–23 age group has the lowest virginity rate and highest Non and Inapplicable rates.' },
  { insight: 'Branch D has the highest Non and Inapplicable rates among youth.' },
  { insight: 'Virginity rate remains mostly stable with minor fluctuations yearly.' },
  { insight: 'Non-virgin rate increases in 2025, indicating a need for stronger youth mentorship programs.' },
];

const healthRetentionInsightsData = [
  { insight: 'Branch D has the highest RA percentage.' },
  { insight: 'Branch D has the highest Pre-RA percentage.' },
  { insight: 'Significant transition from Active to Inactive due to members passing away or leaving the religion.' },
  { insight: 'Mid-year spike in Pre-RA (June) followed by slight recovery in Active members due to retention efforts.' },
  { insight: 'Branch D has the lowest retention rate, suggesting a need for targeted engagement programs.' },
  { insight: 'Branch D has the highest inactive percentage, indicating higher member disengagement or loss.' },
];

// Common Chart.js options for Line and Bar charts
const commonOptions = {
  responsive: true,
  maintainAspectRatio: false,
  animation: {
    duration: 1000,
    easing: 'easeInOutQuart' as 'easeInOutQuart',
    delay: (context: any) => {
      if (context.type === 'data' && context.parsed?.x) {
        return context.parsed.x * 100; // Progressive line animation
      }
      return 0;
    },
  },
  interaction: {
    mode: 'nearest' as 'nearest',
    axis: 'x' as 'x',
    intersect: false,
  },
  scales: {
    x: {
      ticks: { font: { size: 9.6 } },
      grid: { display: true, borderDash: [5, 5] },
    },
    y: {
      ticks: { font: { size: 9.6 } },
      grid: { display: true, borderDash: [5, 5] },
    },
  },
  plugins: {
    tooltip: { bodyFont: { size: 9.6 } },
    legend: {
      labels: {
        font: { size: 9.6 },
        usePointStyle: true,
        pointStyle: 'circle',
        padding: 10,
      },
    },
  },
};

// Pie chart-specific options
const pieOptions = {
  responsive: true,
  maintainAspectRatio: false,
  animation: {
    animateRotate: true,
    animateScale: true,
    duration: 1000,
    easing: 'easeInOutQuart' as 'easeInOutQuart',
  },
  plugins: {
    tooltip: { bodyFont: { size: 9.6 } },
    legend: {
      labels: {
        font: { size: 9.6 },
        usePointStyle: true,
        pointStyle: 'circle',
        padding: 10,
      },
    },
  },
};

// Error Boundary
class ErrorBoundary extends Component<{ children: ReactNode }, { hasError: boolean; error: Error | null }> {
  constructor(props: { children: ReactNode }) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: Error) {
    return { hasError: true, error };
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="min-h-screen flex items-center justify-center bg-gray-100">
          <div className="bg-white p-4 rounded-lg shadow-lg">
            <h2 className="text-lg font-semibold text-red-600">Error in Dashboard</h2>
            <p className="text-sm text-gray-600">{this.state.error?.message}</p>
            <button
              className="mt-4 bg-[#22C55E] text-white px-4 py-2 rounded"
              onClick={() => window.location.reload()}
            >
              Reload Page
            </button>
          </div>
        </div>
      );
    }
    return this.props.children;
  }
}

const Dashboard: React.FC = () => {
  const [dataLoaded, setDataLoaded] = useState<boolean>(false);
  const worldMapRef = useRef<HTMLDivElement>(null);

  // Handle header click
  const handleHeaderClick = useCallback(() => {
    console.log('Dashboard: Header clicked');
    // Placeholder for navigation action (e.g., redirect, toggle menu)
    // Example: window.location.href = '/home';
  }, []);

  // Debug logs
  useEffect(() => {
    console.log('Dashboard: Mounted');
    return () => console.log('Dashboard: Unmounted');
  }, []);

  // Simulate async data loading
  useEffect(() => {
    console.log('Dashboard: Starting data load');
    const loadData = async () => {
      try {
        await new Promise(resolve => setTimeout(resolve, 1000));
        console.log('Dashboard: Data loaded successfully');
        setDataLoaded(true);
      } catch (error) {
        console.error('Dashboard: Data load failed', error);
      }
    };
    loadData();
  }, []);

  // Log WorldMap container dimensions
  useEffect(() => {
    if (worldMapRef.current) {
      const { width, height } = worldMapRef.current.getBoundingClientRect();
      console.log('Dashboard: WorldMap container dimensions', { width, height });
    }
  }, [dataLoaded]);

  const handleChartClick = useCallback((_event: any, elements: any[], chart: any) => {
    console.time('ChartClick');
    if (elements.length > 0) {
      const { datasetIndex, index } = elements[0];
      const label = chart.data.labels[index];
      const value = chart.data.datasets[datasetIndex].data[index];
      const datasetLabel = chart.data.datasets[datasetIndex].label;
      console.log('Dashboard: Chart clicked', { datasetLabel, label, value });
    }
    console.timeEnd('ChartClick');
  }, []);

  const getCountryColor = useCallback((value: number) => {
    const maxMembers = Math.max(...membersByCountryData.map(data => data.value));
    const minMembers = Math.min(...membersByCountryData.map(data => data.value));
    const ratio = (value - minMembers) / (maxMembers - minMembers || 1);
    const r = Math.round(134 - (134 - 22) * ratio);
    const g = Math.round(239 - (239 - 101) * ratio);
    const b = Math.round(172 - (172 - 52) * ratio);
    return `rgb(${r}, ${g}, ${b})`;
  }, []);

  const fadeVariants = {
    hidden: { opacity: 0, y: 20 },
    visible: { opacity: 1, y: 0, transition: { duration: 0.5, ease: 'easeInOut' as 'easeInOut' } },
  };

  if (!dataLoaded) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-100">
        <div className="bg-white p-6 rounded-lg shadow-md flex flex-col items-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-[#22C55E] mb-4"></div>
          <span className="text-gray-600">Loading dashboard data...</span>
        </div>
      </div>
    );
  }

  return (
    <ErrorBoundary>
      <div className="min-h-screen bg-gray-100 font-sans">
        <header className="sticky top-0 z-50 w-full bg-white shadow-sm" role="banner">
          <button
            className="w-full h-full flex items-center px-4 py-3 cursor-pointer focus:outline-none"
            style={{ touchAction: 'manipulation' }}
            onClick={handleHeaderClick}
            aria-label="Navigate to dashboard home"
          >
            <img src={MKNLogo} alt="Muzi Ka Nkulunkulu Logo" className="h-8 w-auto" />
            <div className="ml-2 flex flex-col">
              <span className="text-lg font-semibold text-gray-800 tracking-wider">Muzi Ka Nkulunkulu</span>
              <p className="text-[0.7rem] text-gray-400 -mt-0.5 tracking-tight">where illnesses and troubles are cured</p>
            </div>
          </button>
        </header>


        <div className="relative z-10 p-3 bg-gray-100 rounded-lg font-sans pb-45 overflow-auto box-border">
          <section className="mb-16">
            <div className="w-full p-6 mb-4 md:flex md:justify-between md:items-center">
              <div className="flex flex-col items-center md:items-start">
                <h2 className="text-sm font-semibold text-gray-700 flex items-center gap-2 md:text-left">
                  <FiTrendingUp size={14} className="text-[#22C55E]" /> Membership & Growth
                </h2>
                <p className="text-[0.6rem] text-gray-600 mt-1 text-center md:text-left">Visualizes membership growth trends, regional distribution, and country-specific data to identify expansion opportunities.</p>
              </div>
            </div>
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
              <motion.div
                className="bg-white p-3 pt-4 rounded-lg shadow overflow-hidden"
                initial="hidden"
                whileInView="visible"
                viewport={{ once: true, margin: '-100px' }}
                variants={fadeVariants}
              >
                <h3 className="text-[0.55rem] font-medium text-gray-800 uppercase tracking-wider mb-2 whitespace-nowrap text-left">1,700,000 MEMBERS WORLD WIDE</h3>
                <div ref={worldMapRef} className="w-full h-[200px] flex justify-center items-center">
                  <WorldMap
                    color="#22C55E"
                    backgroundColor="transparent"
                    borderColor="#5D4037"
                    valueSuffix="members"
                    size="responsive"
                    data={membersByCountryData}
                    styleFunction={({ countryValue }: { countryValue?: number }) => ({
                      fill: countryValue ? getCountryColor(countryValue) : '#e5e7eb',
                      stroke: '#5D4037',
                      strokeWidth: 1,
                      cursor: 'pointer',
                    })}
                  />
                </div>
              </motion.div>
              <div className="bg-white p-3 pt-4 rounded-lg shadow">
                <h3 className="text-[0.55rem] font-medium text-gray-800 uppercase tracking-wider mb-2 whitespace-nowrap text-left">Membership Growth Over Time</h3>
                <div style={{ width: '100%', height: 200, marginRight: '20px' }}>
                  <Line
                    data={{
                      labels: membershipGrowthData.map(data => data.month),
                      datasets: [
                        {
                          label: 'New Joins',
                          data: membershipGrowthData.map(data => data.newJoins),
                          borderColor: '#22C55E',
                          backgroundColor: '#22C55E',
                          tension: 0.4,
                          borderWidth: 2,
                          pointBackgroundColor: '#22C55E',
                          pointBorderColor: '#22C55E',
                          pointBorderWidth: 0,
                          pointRadius: 4,
                          pointHitRadius: 20,
                        },
                        {
                          label: 'Drop',
                          data: membershipGrowthData.map(data => data.drop),
                          borderColor: '#D32F2F',
                          backgroundColor: '#D32F2F',
                          tension: 0.4,
                          borderWidth: 2,
                          pointBackgroundColor: '#D32F2F',
                          pointBorderColor: '#D32F2F',
                          pointBorderWidth: 0,
                          pointRadius: 4,
                          pointHitRadius: 20,
                        },
                      ],
                    }}
                    options={{ ...commonOptions, onClick: handleChartClick }}
                  />
                </div>
              </div>
              <div className="bg-white p-3 pt-4 rounded-lg shadow">
                <h3 className="text-[0.55rem] font-medium text-gray-800 uppercase tracking-wider mb-2 whitespace-nowrap text-left">Members by Region</h3>
                <div style={{ width: '100%', height: 200, marginRight: '20px' }}>
                  <Bar
                    data={{
                      labels: membersByRegionData.map(data => data.region),
                      datasets: [
                        {
                          label: 'Members',
                          data: membersByRegionData.map(data => data.members),
                          backgroundColor: '#22C55E',
                        },
                      ],
                    }}
                    options={{ ...commonOptions, onClick: handleChartClick }}
                  />
                </div>
              </div>
              <div className="bg-white p-3 pt-4 rounded-lg shadow">
                <h3 className="text-[0.55rem] font-medium text-gray-800 uppercase tracking-wider mb-2 whitespace-nowrap text-left">Members per Branch</h3>
                <div style={{ width: '100%', height: 200, marginRight: '20px' }}>
                  <Bar
                    data={{
                      labels: branchMembersData.map(data => data.branch),
                      datasets: [
                        {
                          label: 'Members',
                          data: branchMembersData.map(data => data.total),
                          backgroundColor: '#22C55E',
                        },
                      ],
                    }}
                    options={{ ...commonOptions, onClick: handleChartClick }}
                  />
                </div>
              </div>
            </div>
            <div className="bg-white p-3 mt-3 rounded-lg shadow">
              <h3 className="text-[0.55rem] font-medium text-gray-800 uppercase tracking-wider mb-2 whitespace-nowrap text-left">Actionable Insights</h3>
              <ul className="text-[0.6rem] text-gray-600 space-y-2">
                {membershipInsightsData.map((insight, index) => (
                  <li key={index} className="flex items-center gap-2">
                    <FiCheckCircle className="text-[#22C55E] h-4 w-4" />
                    {insight.insight}
                  </li>
                ))}
              </ul>
            </div>
          </section>

          <section className="mb-16">
            <div className="w-full p-6 mb-4 md:flex md:justify-between md:items-center">
              <div className="flex flex-col items-center md:items-start">
                <h2 className="text-sm font-semibold text-gray-700 flex items-center gap-2 md:text-left">
                  <FiActivity size={14} className="text-[#22C55E]" /> Attendance Trends
                </h2>
                <p className="text-[0.6rem] text-gray-600 mt-1 text-center md:text-left">Tracks attendance patterns over time, by week, and across branches to optimize engagement strategies.</p>
              </div>
            </div>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
              <div className="bg-white p-3 pt-4 rounded-lg shadow">
                <h3 className="text-[0.55rem] font-medium text-gray-800 uppercase tracking-wider mb-2 whitespace-nowrap text-left">Average Attendance Trends</h3>
                <div style={{ width: '100%', height: 200, marginRight: '20px' }}>
                  <Line
                    data={{
                      labels: attendanceTrendData.map(data => data.date),
                      datasets: [
                        {
                          label: 'Attendance',
                          data: attendanceTrendData.map(data => data.attendance),
                          borderColor: '#22C55E',
                          backgroundColor: '#22C55E',
                          tension: 0.4,
                          borderWidth: 2,
                          pointBackgroundColor: '#22C55E',
                          pointBorderColor: '#22C55E',
                          pointBorderWidth: 0,
                          pointRadius: 4,
                          pointHitRadius: 20,
                        },
                      ],
                    }}
                    options={{ ...commonOptions, onClick: handleChartClick }}
                  />
                </div>
              </div>
              <div className="bg-white p-3 pt-4 rounded-lg shadow">
                <h3 className="text-[0.55rem] font-medium text-gray-800 uppercase tracking-wider mb-2 whitespace-nowrap text-left">Attendance by Day of Week</h3>
                <div style={{ width: '100%', height: 200, marginRight: '20px' }}>
                  <Bar
                    data={{
                      labels: attendanceByDayData.map(data => data.day),
                      datasets: [
                        {
                          label: 'Attendance',
                          data: attendanceByDayData.map(data => data.value),
                          backgroundColor: '#22C55E',
                        },
                      ],
                    }}
                    options={{ ...commonOptions, onClick: handleChartClick }}
                  />
                </div>
              </div>
              <div className="bg-white p-3 pt-4 rounded-lg shadow">
                <h3 className="text-[0.55rem] font-medium text-gray-800 uppercase tracking-wider mb-2 whitespace-nowrap text-left">Branch Attendance Comparison</h3>
                <div style={{ width: '100%', height: 200, marginRight: '20px' }}>
                  <Bar
                    data={{
                      labels: branchAttendanceData.map(data => data.branch),
                      datasets: [
                        {
                          label: 'Attendance Rate',
                          data: branchAttendanceData.map(data => data.attendanceRate),
                          backgroundColor: '#22C55E',
                        },
                      ],
                    }}
                    options={{ ...commonOptions, onClick: handleChartClick }}
                  />
                </div>
              </div>
              <div className="bg-white p-3 pt-4 rounded-lg shadow">
                <h3 className="text-[0.55rem] font-medium text-gray-800 uppercase tracking-wider mb-2 whitespace-nowrap text-left">Attendance by Week</h3>
                <div style={{ width: '100%', height: 200, marginRight: '20px' }}>
                  <Bar
                    data={{
                      labels: attendanceByWeekData.map(data => data.week),
                      datasets: [
                        {
                          label: 'Attendance',
                          data: attendanceByWeekData.map(data => data.value),
                          backgroundColor: '#22C55E',
                        },
                      ],
                    }}
                    options={{ ...commonOptions, onClick: handleChartClick }}
                  />
                </div>
              </div>
            </div>
            <div className="bg-white p-3 mt-3 rounded-lg shadow">
              <h3 className="text-[0.55rem] font-medium text-gray-800 uppercase tracking-wider mb-2 whitespace-nowrap text-left">Actionable Insights</h3>
              <ul className="text-[0.6rem] text-gray-600 space-y-2">
                {attendanceInsightsData.map((insight, index) => (
                  <li key={index} className="flex items-center gap-2">
                    <FiCheckCircle className="text-[#22C55E] h-4 w-4" />
                    {insight.insight}
                  </li>
                ))}
              </ul>
            </div>
          </section>

          <section className="mb-16">
            <div className="w-full p-6 mb-4 md:flex md:justify-between md:items-center">
              <div className="flex flex-col items-center md:items-start">
                <h2 className="text-sm font-semibold text-gray-700 flex items-center gap-2 md:text-left">
                  <FiUsers size={14} className="text-[#22C55E]" /> Youth & Purity Insights
                </h2>
                <p className="text-[0.6rem] text-gray-600 mt-1 text-center md:text-left">Analyzes youth purity metrics (Virgin: youths who are virgins; Non: youths who are not virgins; Inapplicable: parents, married, etc.) by status, age, and branch to guide mentorship programs.</p>
              </div>
            </div>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
              <div className="bg-white p-3 pt-4 rounded-lg shadow">
                <h3 className="text-[0.55rem] font-medium text-gray-800 uppercase tracking-wider mb-2 whitespace-nowrap text-left">Virginity Status (August)</h3>
                <div style={{ width: '100%', height: 200, marginRight: '20px' }}>
                  <Pie
                    data={{
                      labels: virginityStatusData.map(data => data.name),
                      datasets: [
                        {
                          data: virginityStatusData.map(data => data.value),
                          backgroundColor: ['#22C55E', '#F57C00', '#9E9E9E'],
                        },
                      ],
                    }}
                    options={pieOptions}
                  />
                </div>
              </div>
              <div className="bg-white p-3 pt-4 rounded-lg shadow">
                <h3 className="text-[0.55rem] font-medium text-gray-800 uppercase tracking-wider mb-2 whitespace-nowrap text-left">Virginity Trend (Yearly)</h3>
                <div style={{ width: '100%', height: 200, marginRight: '20px' }}>
                  <Line
                    data={{
                      labels: virginityTrendData.map(data => data.year),
                      datasets: [
                        {
                          label: 'Virgin',
                          data: virginityTrendData.map(data => data.virgin),
                          borderColor: '#22C55E',
                          backgroundColor: '#22C55E',
                          tension: 0.4,
                          borderWidth: 2,
                          pointBackgroundColor: '#22C55E',
                          pointBorderColor: '#22C55E',
                          pointBorderWidth: 0,
                          pointRadius: 4,
                          pointHitRadius: 20,
                        },
                        {
                          label: 'Non',
                          data: virginityTrendData.map(data => data.non),
                          backgroundColor: '#F57C00',
                          borderColor: '#F57C00',
                          tension: 0.4,
                          borderWidth: 2,
                          pointBackgroundColor: '#F57C00',
                          pointBorderColor: '#F57C00',
                          pointBorderWidth: 0,
                          pointRadius: 4,
                          pointHitRadius: 20,
                        },
                        {
                          label: 'Inapplicable',
                          data: virginityTrendData.map(data => data.inapplicable),
                          borderColor: '#9E9E9E',
                          backgroundColor: '#9E9E9E',
                          tension: 0.4,
                          borderWidth: 2,
                          pointBackgroundColor: '#9E9E9E',
                          pointBorderColor: '#9E9E9E',
                          pointBorderWidth: 0,
                          pointRadius: 4,
                          pointHitRadius: 20,
                        },
                      ],
                    }}
                    options={{ ...commonOptions, onClick: handleChartClick }}
                  />
                </div>
              </div>
              <div className="bg-white p-3 pt-4 rounded-lg shadow">
                <h3 className="text-[0.55rem] font-medium text-gray-800 uppercase tracking-wider mb-2 whitespace-nowrap text-left">Virginity by Age Group</h3>
                <div style={{ width: '100%', height: 200, marginRight: '20px' }}>
                  <Bar
                    data={{
                      labels: virginityByAgeData.map(data => data.ageGroup),
                      datasets: [
                        {
                          label: 'Virgin',
                          data: virginityByAgeData.map(data => data.virgin),
                          backgroundColor: '#22C55E',
                          stack: 'Stack 0',
                        },
                        {
                          label: 'Non',
                          data: virginityByAgeData.map(data => data.non),
                          backgroundColor: '#F57C00',
                          stack: 'Stack 0',
                        },
                        {
                          label: 'Inapplicable',
                          data: virginityByAgeData.map(data => data.inapplicable),
                          backgroundColor: '#9E9E9E',
                          stack: 'Stack 0',
                        },
                      ],
                    }}
                    options={{ ...commonOptions, onClick: handleChartClick, scales: { ...commonOptions.scales, x: { stacked: true }, y: { stacked: true } } }}
                  />
                </div>
              </div>
              <div className="bg-white p-3 pt-4 rounded-lg shadow">
                <h3 className="text-[0.55rem] font-medium text-gray-800 uppercase tracking-wider mb-2 whitespace-nowrap text-left">Virginity by Branch</h3>
                <div style={{ width: '100%', height: 200, marginRight: '20px' }}>
                  <Bar
                    data={{
                      labels: virginityByBranchData.map(data => data.branch),
                      datasets: [
                        {
                          label: 'Virgin',
                          data: virginityByBranchData.map(data => data.virgin),
                          backgroundColor: '#22C55E',
                          stack: 'Stack 0',
                        },
                        {
                          label: 'Non',
                          data: virginityByBranchData.map(data => data.non),
                          backgroundColor: '#F57C00',
                          stack: 'Stack 0',
                        },
                        {
                          label: 'Inapplicable',
                          data: virginityByBranchData.map(data => data.inapplicable),
                          backgroundColor: '#9E9E9E',
                          stack: 'Stack 0',
                        },
                      ],
                    }}
                    options={{ ...commonOptions, onClick: handleChartClick, scales: { ...commonOptions.scales, x: { stacked: true }, y: { stacked: true } } }}
                  />
                </div>
              </div>
            </div>
            <div className="bg-white p-3 mt-3 rounded-lg shadow">
              <h3 className="text-[0.55rem] font-medium text-gray-800 uppercase tracking-wider mb-2 whitespace-nowrap text-left">Actionable Insights</h3>
              <ul className="text-[0.6rem] text-gray-600 space-y-2">
                {purityInsightsData.map((insight, index) => (
                  <li key={index} className="flex items-center gap-2">
                    <FiCheckCircle className="text-[#22C55E] h-4 w-4" />
                    {insight.insight}
                  </li>
                ))}
              </ul>
            </div>
          </section>

          <section className="mb-16">
            <div className="w-full p-6 mb-4 md:flex md:justify-between md:items-center">
              <div className="flex flex-col items-center md:items-start">
                <h2 className="text-sm font-semibold text-gray-700 flex items-center gap-2 md:text-left">
                  <FiAlertTriangle size={14} className="text-[#22C55E]" /> Health & Retention Insights
                </h2>
                <p className="text-[0.6rem] text-gray-600 mt-1 text-center md:text-left">Monitors retention rates and health metrics, including RA and Pre-RA, to identify at-risk members.</p>
              </div>
            </div>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
              <div className="bg-white p-3 pt-4 rounded-lg shadow">
                <h3 className="text-[0.55rem] font-medium text-gray-800 uppercase tracking-wider mb-2 whitespace-nowrap text-left">Retention Funnel Proportions</h3>
                <div style={{ width: '100%', height: 200, marginRight: '20px' }}>
                  <Bar
                    data={{
                      labels: retentionFunnelData.map(data => data.stage),
                      datasets: [
                        {
                          label: 'Count',
                          data: retentionFunnelData.map(data => data.count),
                          backgroundColor: ['#22C55E', '#9E9E9E', '#F57C00', '#D32F2F'],
                        },
                      ],
                    }}
                    options={{ ...commonOptions, onClick: handleChartClick }}
                  />
                </div>
              </div>
              <div className="bg-white p-3 pt-4 rounded-lg shadow">
                <h3 className="text-[0.55rem] font-medium text-gray-800 uppercase tracking-wider mb-2 whitespace-nowrap text-left">RA & Pre-RA Trend</h3>
                <div style={{ width: '100%', height: 200, marginRight: '20px' }}>
                  <Line
                    data={{
                      labels: raPreRATrendData.map(data => data.month),
                      datasets: [
                        {
                          label: 'Active',
                          data: raPreRATrendData.map(data => data.active),
                          borderColor: '#22C55E',
                          backgroundColor: '#22C55E',
                          tension: 0.4,
                          borderWidth: 2,
                          pointBackgroundColor: '#22C55E',
                          pointBorderColor: '#22C55E',
                          pointBorderWidth: 0,
                          pointRadius: 4,
                          pointHitRadius: 20,
                        },
                        {
                          label: 'Inactive',
                          data: raPreRATrendData.map(data => data.inactive),
                          borderColor: '#9E9E9E',
                          backgroundColor: '#9E9E9E',
                          tension: 0.4,
                          borderWidth: 2,
                          pointBackgroundColor: '#9E9E9E',
                          pointBorderColor: '#9E9E9E',
                          pointBorderWidth: 0,
                          pointRadius: 4,
                          pointHitRadius: 20,
                        },
                        {
                          label: 'Pre-RA',
                          data: raPreRATrendData.map(data => data.preRA),
                          borderColor: '#F57C00',
                          backgroundColor: '#F57C00',
                          tension: 0.4,
                          borderWidth: 2,
                          pointBackgroundColor: '#F57C00',
                          pointBorderColor: '#F57C00',
                          pointBorderWidth: 0,
                          pointRadius: 4,
                          pointHitRadius: 20,
                        },
                        {
                          label: 'RA',
                          data: raPreRATrendData.map(data => data.ra),
                          borderColor: '#D32F2F',
                          backgroundColor: '#D32F2F',
                          tension: 0.4,
                          borderWidth: 2,
                          pointBackgroundColor: '#D32F2F',
                          pointBorderColor: '#D32F2F',
                          pointBorderWidth: 0,
                          pointRadius: 4,
                          pointHitRadius: 20,
                        },
                      ],
                    }}
                    options={{ ...commonOptions, onClick: handleChartClick }}
                  />
                </div>
              </div>
              <div className="bg-white p-3 pt-4 rounded-lg shadow">
                <h3 className="text-[0.55rem] font-medium text-gray-800 uppercase tracking-wider mb-2 whitespace-nowrap text-left">Branch Health Alerts</h3>
                <div style={{ width: '100%', height: 200, marginRight: '20px' }}>
                  <Bar
                    data={{
                      labels: branchAlertsData.map(data => data.branch),
                      datasets: [
                        {
                          label: 'RA',
                          data: branchAlertsData.map(data => data.raPercentage),
                          backgroundColor: '#D32F2F',
                          stack: 'Stack 0',
                        },
                        {
                          label: 'Pre-RA',
                          data: branchAlertsData.map(data => data.preRAPercentage),
                          backgroundColor: '#F57C00',
                          stack: 'Stack 0',
                        },
                        {
                          label: 'Attendance',
                          data: branchAlertsData.map(data => data.attendanceRate),
                          backgroundColor: '#22C55E',
                          stack: 'Stack 0',
                        },
                        {
                          label: 'Inactive',
                          data: branchAlertsData.map(data => data.inactivePercentage),
                          backgroundColor: '#9E9E9E',
                          stack: 'Stack 0',
                        },
                      ],
                    }}
                    options={{ ...commonOptions, onClick: handleChartClick, scales: { ...commonOptions.scales, x: { stacked: true }, y: { stacked: true } } }}
                  />
                </div>
              </div>
              <div className="bg-white p-3 pt-4 rounded-lg shadow">
                <h3 className="text-[0.55rem] font-medium text-gray-800 uppercase tracking-wider mb-2 whitespace-nowrap text-left">Retention Rate by Branch</h3>
                <div style={{ width: '100%', height: 200, marginRight: '20px' }}>
                  <Bar
                    data={{
                      labels: branchRetentionData.map(data => data.branch),
                      datasets: [
                        {
                          label: 'Retention Rate (%)',
                          data: branchRetentionData.map(data => data.retentionRate),
                          backgroundColor: '#22C55E',
                        },
                      ],
                    }}
                    options={{ ...commonOptions, onClick: handleChartClick }}
                  />
                </div>
              </div>
            </div>
            <div className="bg-white p-3 mt-3 rounded-lg shadow">
              <h3 className="text-[0.55rem] font-medium text-gray-800 uppercase tracking-wider mb-2 whitespace-nowrap text-left">Actionable Insights</h3>
              <ul className="text-[0.6rem] text-gray-600 space-y-2">
                {healthRetentionInsightsData.map((insight, index) => (
                  <li key={index} className="flex items-center gap-2">
                    <FiCheckCircle className="text-[#22C55E] h-4 w-4" />
                    {insight.insight}
                  </li>
                ))}
              </ul>
            </div>
          </section>
        </div>

      </div>
    </ErrorBoundary>
  );
};

export default Dashboard;

