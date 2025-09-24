
🌍 Muzi Ka Nkulunkulu Management System (MKN-MS)

Muzi Ka Nkulunkulu Management System (MKN-MS) is a modern, web-based platform built with React, TypeScript, and Vite.
It serves as the official management tool for the Muzi Ka Nkulunkulu Religion, providing digital oversight of members, duties, attendance, and growth analytics across branches and regions.

The system starts with a single-branch MVP but is designed to scale globally as the community grows.


---

🚀 Quickstart Guide (Admins)

1. Log In & Navigate

Open the app in your browser.

Enter admin credentials.

The Dashboard shows stats like total members and RA statuses.

Use the bottom navigation menu for sections: Dashboard, Attendance, Members, etc.


2. Register Members

Go to Members → Add New Member.

Enter details (Full Name, Surname, Card Number, Position, Branch).

Click Save.


3. Mark Attendance

Open Attendance.

Toggle presence beside a member’s name.

The system timestamps attendance and auto-finalizes after service.


4. Assign Duties

Go to Roster (MVP feature).

Duty rules enforced:

Izithunywa → only Messengers (exclusive).

Omele Abavangeli & Izaziso → only Evangelists (Evangelists may also hold Umgcini sihlalo).

Umkhokheli phakathi & Umkhokheli phandle → only female Facilitators.




---

✨ Core Features (MVP)

📋 Member Management
Profiles with card number, name, surname, position, and branch.

⛪ Duty Roster
Assign duties with position- and gender-based rules.

📊 Attendance Register

Toggle presence during service.

Auto-finalizes after service ends.

RA system:

Pre-RA (Orange) → 83–89 days absent.

RA (Red) → 90+ days absent.

RA removal requires a logged reason.

3 RA strikes = permanent removal.


Guest attendance supported.


📈 Dashboards

Total Members

RA Members

Pre-RA Members

Future: Age groups, regional trends




---

🔮 Roadmap (Beyond MVP)

Multi-branch support with timezone-based service windows

NFC attendance check-in

Public dashboards for outreach

Role-based access control (Admin, Branch Leader, Viewer)

Exportable reports (PDF/Excel)

Integrations with Yotaabo Smart Projects



---

🛠️ Tech Stack

Frontend: React + TypeScript + Vite

Styling: TailwindCSS

State Management: React Hooks (scalable to Context/Redux)

Tooling: ESLint, TypeScript

Deployment: Vercel/Netlify (static) or Docker



---

⚙️ Installation & Setup

# Clone repo
git clone https://github.com/Yothabo/MKN-ms.git
cd MKN-ms

# Install dependencies
npm install

# Run dev server
npm run dev

# Build for production
npm run build

# Preview build
npm run preview


---

📂 Project Structure

MKN-ms/
├── README.md
├── legacy-packages.md
├── package.json
├── vite.config.ts
├── tsconfig.json
├── tsconfig.app.json
├── tsconfig.node.json
├── tailwind.config.js
├── postcss.config.js
├── eslint.config.js
├── index.html
├── public/
├── src/
│   ├── App.css
│   ├── App.tsx
│   ├── main.tsx
│   ├── assets/
│   ├── components/
│   ├── hooks/
│   ├── utils/
│   ├── styles/
│   └── pages/
│       ├── admin/
│       │   ├── dashboard/
│       │   ├── members/
│       │   ├── duties/
│       │   ├── attendance/
│       │   └── ...
│       └── auth/        # Future
└── dist/                # Production build


---

🧪 Development Notes

ESLint configured for TypeScript + React best practices.

TailwindCSS for utility-first styling.

Legacy dependencies tracked in legacy-packages.md.

Some MVP UI elements are placeholders.



---

👥 Contributing

1. Fork the repo


2. Create a feature branch


3. Submit a pull request



Please follow coding standards and respect the religious context of Muzi Ka Nkulunkulu.


---

📜 License

MIT License. See LICENSE.


---

🧠 About Muzi Ka Nkulunkulu

Muzi Ka Nkulunkulu is a religion with unique traditions and practices.
MKN-MS is built to preserve those values while embracing modern tools to support members worldwide.

