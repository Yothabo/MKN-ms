# 🌍 Muzi Ka Nkulunkulu Management System (MKN-MS)

The **Muzi Ka Nkulunkulu Management System (MKN-MS)** is a modern, web-based platform built with **React, TypeScript, and Vite**. It serves as the official management tool for the Muzi Ka Nkulunkulu religion, providing digital oversight of members, duties, attendance, and growth analytics across branches and regions.

The system begins with a single-branch MVP but is designed to scale globally as the community grows.

---

## 🚀 Quickstart Guide for Admins

This guide will get you started with the most common administrative tasks.

### 1. Log In & Navigate
1. Open the application in your browser and enter your admin credentials.
2. The **Dashboard** provides an overview of key statistics like total members and RA statuses.
3. Use the bottom navigation menu to switch between sections like **Dashboard**, **Attendance**, and **Members**.

### 2. Register Members
1. Go to the **Members** section and click **Add New Member**.
2. Fill in the required details: Full Name, Surname, Card Number, Position, and Branch.
3. Click **Save** to add the new member.

### 3. Mark Attendance
1. Open the **Attendance** section.
2. Toggle the button next to a member’s name to mark their presence.
3. The system timestamps each attendance and automatically finalizes the record after service ends.

### 4. Assign Duties
1. Go to the **Roster** section (MVP feature).
2. The system enforces duty rules based on a member's position and gender:
    - *Izithunywa* → Messengers only (exclusive duty).
    - *Omele Abavangeli* & *Izaziso* → Evangelists only.
    - *Umkhokheli phakathi* & *Umkhokheli phandle* → Female Facilitators only.

---

## ✨ Core Features (MVP)

- **Member Management**
  Create, update, and view member profiles with essential details like card number, name, surname, position, and branch.

- **Duty Roster**
  Assign religious duties with built-in rules to ensure proper assignment.

- **Attendance Register**
  - Track attendance during active service windows.
  - Automatically finalize records after a service ends.
  - **RA (Red Alert) System:**
    - **Pre-RA (Orange)** is triggered after 83–89 days of absence.
    - **RA (Red)** is triggered after 90 days of absence.
    - RA removal requires a logged reason.
    - A member is permanently removed after 3 RA strikes.
  - Supports guest attendance tracking.

- **Dashboards**
  View core metrics including:
  - Total Members
  - RA Members
  - Pre-RA Members
  - Future analytics will include age groups and regional trends.

---

## 🔮 Roadmap (Beyond MVP)

- Multi-branch support with timezone-based service windows
- NFC-based attendance check-in
- Public dashboards for transparency and outreach
- Role-based access control (Admin, Branch Leader, Viewer)
- Exportable reports (PDF/Excel)
- Integrations with other Yotaabo Smart Projects

---

## 🛠️ Tech Stack

- **Frontend:** React + TypeScript + Vite
- **Styling:** TailwindCSS
- **State Management:** React Hooks (with scalability to Context/Redux)
- **Tooling:** ESLint, TypeScript
- **Deployment:** Static hosting (Vercel/Netlify) or Docker

---

## ⚙️ Installation & Setup

```bash
# Clone the repository
git clone [https://github.com/Yothabo/MKN-ms.git](https://github.com/Yothabo/MKN-ms.git)
cd MKN-ms

# Install dependencies
npm install

# Run the development server
npm run dev

# Build for production
npm run build

# Preview the production build
npm run preview

