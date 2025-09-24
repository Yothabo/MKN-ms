
# 🌍 Muzi Ka Nkulunkulu Management System (MKN-MS)

**Muzi Ka Nkulunkulu Management System (MKN-MS)** is a modern, web-based platform built with React, TypeScript, and Vite.  
It serves as the **official management tool for the Muzi Ka Nkulunkulu Religion**, enabling digital oversight of members, duties, attendance, and growth analytics across regions and branches.

The system is designed with scalability in mind — starting with one branch for the MVP, but capable of expanding globally as the religion grows.

---

## 🚀 Quickstart Guide for Admins

This guide will help you get started with the Muzi Ka Nkulunkulu Management System (MKN-MS).  
It covers the most common tasks you will need to perform.

### Step 1: Log In and Navigate
- Open the application in your web browser.  
- Enter your admin credentials to log in.  
- You will land on the **Dashboard**, where you can see an overview of key statistics like total members and RA statuses.  
- Use the navigation menu at the bottom to move between sections: Dashboard, Attendance, Members, etc.  

### Step 2: Register a New Member
- Navigate to the **Members** section.  
- Click the **Add New Member** button.  
- Fill in details: Full Name, Surname, Card Number, and other relevant info.  
- Select the correct **Position** and **Main Branch** from the dropdown lists.  
- Click **Save** to add the new member.  

### Step 3: Mark Attendance
- Go to the **Attendance** section.  
- You will see a list of all members.  
- Locate a member and toggle the button next to their name to mark attendance.  
- The button will change color to confirm presence.  
- The system automatically records date/time and finalizes attendance at the end of service.  

### Step 4: Assign Duties
- Go to the **Roster** section (coming soon in the MVP).  
- The system enforces rules for duty assignments:
  - *Izithunywa* → only Messengers.  
  - *Omele Abavangeli* & *Izaziso* → only Evangelists.  
  - *Umkhokheli phakathi* & *Umkhokheli phandle* → only female Facilitators.  
- Select a member → choose an available duty.  
- Confirm the assignment to update the roster.  

---

## ✨ Core Features (MVP)

- 📋 **Member Management**  
  Register, update, and view member profiles (card number, name, surname, position, branch).

- ⛪ **Duty Roster**  
  Assign and manage duties according to rules:  
  - *Izithunywa* → only Messengers (exclusive).  
  - *Omele Abavangeli* & *Izaziso* → only Evangelists (Evangelists may also hold *Umgcini sihlalo*).  
  - *Umkhokheli phakathi* & *Umkhokheli phandle* → only female Facilitators.  
  - Others: *Umgcini sihlalo*, *Obalayo*, *Ofunda imfundiso*.  

- 📊 **Attendance Register**  
  - Toggle presence during service.  
  - Auto-finalize after service ends.  
  - RA (Red Alert) system:  
    - Pre-RA (Orange) → 83–89 days absence.  
    - RA (Red) → 90 days absence.  
    - RA requires logged reason for removal.  
    - 3 RA strikes = permanent removal.  
  - Guest attendance supported.  

- 📈 **Dashboards**  
  - Total Members  
  - RA Members  
  - Pre-RA Members  
  - Age group insights (future)  
  - Regional/branch analytics (future)  

---

## 🔮 Roadmap (Beyond MVP)

- 🌍 Multi-branch support with timezone-based service times.  
- 📱 NFC attendance check-in.  
- 📊 Public dashboard for transparency and outreach.  
- 🛡️ Role-based access control (Admin, Branch Leader, Viewer).  
- 📑 Export reports (PDF/Excel).  
- 🔗 Integrations with Yotaabo Smart Projects.  

---

## 🛠️ Tech Stack

- **Frontend:** React + TypeScript + Vite  
- **Styling:** TailwindCSS  
- **State:** React Hooks (scalable to Redux/Context)  
- **Build Tooling:** Vite, ESLint, TypeScript  
- **Deployment:** Netlify/Vercel (static) or Docker (optional)  

---

## ⚙️ Installation & Setup

Clone repository:
```bash
git clone
cd MKN-ms

Install dependencies:

npm install

Run in development:

npm run dev

Build for production:

npm run build

Preview production build:

npm run preview


---

📂 Project Structure

MKN-ms/
├── README.md                  # Project documentation
├── legacy-packages.md         # Archive of unused/removed dependencies
├── package.json               # Metadata & dependencies
├── vite.config.ts             # Vite config
├── tsconfig.json              # Root TS config
├── tsconfig.app.json          # TS config for app source
├── tsconfig.node.json         # TS config for node scripts
├── tailwind.config.js         # Tailwind config
├── postcss.config.js          # PostCSS config
├── eslint.config.js           # ESLint config
├── index.html                 # Main entry
├── public/                    # Static assets
├── src/                       # Application source
│   ├── App.css                # Global styles
│   ├── App.tsx                # Root component
│   ├── main.tsx               # Entry point
│   ├── assets/                # Images/icons
│   ├── components/            # Reusable components
│   ├── hooks/                 # Custom hooks
│   ├── utils/                 # Helpers/constants
│   ├── styles/                # Styling modules
│   └── pages/                 # App pages
│       ├── admin/             
│       │   ├── dashboard/     # Dashboards
│       │   ├── members/       # Member management
│       │   ├── duties/        # Duty roster
│       │   ├── attendance/    # Attendance
│       │   └── ...
│       └── auth/              # Authentication (future)
└── dist/                      # Production build


---

🧪 Development Notes

ESLint set up for TypeScript + React best practices.

TailwindCSS for utility-first styling.

Legacy dependencies tracked in legacy-packages.md.

Some MVP UI elements are mock placeholders.



---

👥 Contributing

Contributions are welcome.
Steps:

1. Fork the repo


2. Create a feature branch


3. Submit a pull request



Please follow coding standards and respect the religious context of Muzi Ka Nkulunkulu.



📜 License

MIT License. See LICENSE for details.


🧠 About Muzi Ka Nkulunkulu

Muzi Ka Nkulunkulu is a Religion with unique traditions, practices, and organizational needs.
MKN-MS preserves those values while embracing modern digital tools to manage growth and serve members worldwide.

