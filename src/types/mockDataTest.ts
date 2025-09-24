
export interface Member {
  id: string;
  names: string[];
  surname: string;
  cardNumber: number | string;
  dob: Date;
  virginityStatus: "unset" | "virgin" | "non-virgin" | "inapplicable";
  dateOfEntry: Date;
  reasonOfEntry: string;
  gender: "unset" | "male" | "female" | "other";
  position: string;
  mainBranch: "unset" | "Johannesburg" | "Bulawayo" | "Gaborone" | "Harare" | "Ireland";
  phone: string;
  email: string;
  otpPreference: "unset" | "phone" | "email";
  password: string;
  role: "member" | "admin" | "super_admin";
  lastSeenOffsetDays: number;
  raCount: number;
  status: "Active" | "Pre-RA" | "RA" | "Inactive";
  reAdmissionOffsetDays: number | null;
  raHistory: {
    dateOffsetDays: number;
    reason: string;
    removedDate: string | null;
    removedReason: string | null;
  }[];
  trackingActive: boolean;
  preRaWarningSentOffsetDays: number | null;
  globalLock: boolean;
  lockStatus: "Unlocked" | "RA Lock" | "Warning" | "Deceased" | "Untracked";
  attendance: {
    dateOffsetDays: number;
    branch: "unset" | "Johannesburg" | "Bulawayo" | "Gaborone" | "Harare" | "Ireland";
    type: "Regular" | "Guest";
    timestampOffsetHours: number;
    markedBy: string;
  }[];
  selfManagementData: {
    messages: string[];
    attendanceSummary: {
      totalAttendances: number;
      last90Days: number;
      raCount: number;
    };
  };
  deceasedInfo?: {
    dateOfDeath: Date;
    reason: string;
    memorialServiceDate: Date;
    deathCertificateNumber?: string;
  };
  lastLogin?: Date;
  lastPasswordChange?: Date;
  failedLoginAttempts?: number;
  accountActivated?: boolean;
  profilePicture?: string;
  emergencyContact?: {
    name: string;
    relationship: string;
    phone: string;
  };
  notes?: string[];
  permissions?: {
    canManageAttendance?: boolean;
    canManageMembers?: boolean;
    canManageFinance?: boolean;
  };
}

export interface ExtendedMember extends Member {
  age: number;
  name: string;
  residentialAddress: string;
  phoneNumber: string;
}

export interface GuestMember {
  visitedBranch: string;
  timestamp: number;
  id: string;
  names: string[];
  surname: string;
  cardNumber: string | number;
  dob: Date;
}

// NodeJS namespace declaration
declare global {
  namespace NodeJS {
    interface Timeout {}
    interface Immediate {}
  }
}

// motion variants
export interface MotionVariants {
  hidden: { opacity: number; y: number; scale: number };
  visible: { 
    opacity: number; 
    y: number; 
    scale: number;
    transition: { 
      duration: number; 
      type: "spring" | "tween";
      bounce?: number;
    };
  };
  exit: { 
    opacity: number; 
    y: number; 
    scale: number;
    transition: { 
      duration: number;
      type: "spring" | "tween";
    };
  };
}

export const fieldVariants: MotionVariants = {
  hidden: { opacity: 0, y: 10, scale: 0.95 },
  visible: { 
    opacity: 1, 
    y: 0, 
    scale: 1,
    transition: { 
      duration: 0.3, 
      type: "spring",
      bounce: 0.2 
    } 
  },
  exit: { 
    opacity: 0, 
    y: -10, 
    scale: 0.95,
    transition: { 
      duration: 0.2,
      type: "tween"
    } 
  }
};

export const getCardNumberAsString = (cardNumber: string | number): string => {
  return String(cardNumber);
};

export const mockDataTest: { members: Member[] } = {

  members: [
    {
      id: "mem001",
      names: ["Thabiso"],
      surname: "Molefe",
      cardNumber: 1001,
      dob: new Date("1983-07-14"), // 40 years old
      virginityStatus: "inapplicable", // Evangelist position
      dateOfEntry: new Date("2020-03-01"),
      reasonOfEntry: "Deliverance from spiritual oppression",
      gender: "male",
      position: "Evangelist",
      mainBranch: "Johannesburg",
      phone: "+27823456701",
      email: "thabiso.molefe@church.org",
      otpPreference: "email",
      password: "hashedPassword1",
      role: "member",
      lastSeenOffsetDays: 12,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 12,
          branch: "Johannesburg",
          type: "Regular",
          timestampOffsetHours: 240,
          markedBy: "admin001"
        },
        {
          dateOffsetDays: 30,
          branch: "Bulawayo",
          type: "Guest",
          timestampOffsetHours: 180,
          markedBy: "admin002"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Thabiso!"],
        attendanceSummary: {
          totalAttendances: 18,
          last90Days: 6,
          raCount: 0
        }
      }
    },
    {
      id: "mem002",
      names: ["Lerato"],
      surname: "Mokoena",
      cardNumber: 1002,
      dob: new Date("1995-11-22"), // 28 years old (13-35 range)
      virginityStatus: "non-virgin", // Not Evangelist/Holy Messenger
      dateOfEntry: new Date("2021-06-15"),
      reasonOfEntry: "Healing from spiritual affliction",
      gender: "female",
      position: "Member",
      mainBranch: "Bulawayo",
      phone: "+27823456702",
      email: "lerato.mokoena@church.org",
      otpPreference: "phone",
      password: "hashedPassword2",
      role: "member",
      lastSeenOffsetDays: 20,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 20,
          branch: "Bulawayo",
          type: "Regular",
          timestampOffsetHours: 180,
          markedBy: "admin002"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Lerato!"],
        attendanceSummary: {
          totalAttendances: 22,
          last90Days: 7,
          raCount: 0
        }
      }
    },
    {
      id: "mem003",
      names: ["Kgaogelo"],
      surname: "Ndlovu",
      cardNumber: 1003,
      dob: new Date("2003-04-09"), // 20 years old (13-35 range)
      virginityStatus: "inapplicable",
      dateOfEntry: new Date("2022-01-10"),
      reasonOfEntry: "Answered spiritual calling",
      gender: "female",
      position: "Holy Messenger",
      mainBranch: "Gaborone",
      phone: "+27823456703",
      email: "k.ndlovu@church.org",
      otpPreference: "email",
      password: "hashedPassword3",
      role: "member",
      lastSeenOffsetDays: 5,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 5,
          branch: "Gaborone",
          type: "Regular",
          timestampOffsetHours: 120,
          markedBy: "admin003"
        },
        {
          dateOffsetDays: 15,
          branch: "Harare",
          type: "Guest",
          timestampOffsetHours: 90,
          markedBy: "admin001"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Kgaogelo!"],
        attendanceSummary: {
          totalAttendances: 12,
          last90Days: 4,
          raCount: 0
        }
      }
    },
    {
      id: "mem004",
      names: ["Palesa"],
      surname: "Dlamini",
      cardNumber: 1004,
      dob: new Date("1982-09-30"), // 41 years old
      virginityStatus: "inapplicable", // Over 35
      dateOfEntry: new Date("2020-08-20"),
      reasonOfEntry: "Deliverance from ancestral spirits",
      gender: "female",
      position: "Facilitator",
      mainBranch: "Harare",
      phone: "+27823456704",
      email: "palesa.dlamini@church.org",
      otpPreference: "email",
      password: "hashedPassword4",
      role: "admin",
      lastSeenOffsetDays: 15,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 15,
          branch: "Harare",
          type: "Regular",
          timestampOffsetHours: 200,
          markedBy: "admin004"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Palesa!"],
        attendanceSummary: {
          totalAttendances: 14,
          last90Days: 5,
          raCount: 0
        }
      }
    },
    {
      id: "mem005",
      names: ["Mpho"],
      surname: "Khumalo",
      cardNumber: 1005,
      dob: new Date("1975-02-17"), // 48 years old
      virginityStatus: "inapplicable", // Over 35
      dateOfEntry: new Date("2019-11-05"),
      reasonOfEntry: "Freedom from spiritual bondage",
      gender: "male",
      position: "Facilitator",
      mainBranch: "Ireland",
      phone: "+27823456705",
      email: "mpho.khumalo@church.org",
      otpPreference: "phone",
      password: "hashedPassword5",
      role: "admin",
      lastSeenOffsetDays: 30,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 30,
          branch: "Ireland",
          type: "Regular",
          timestampOffsetHours: 300,
          markedBy: "admin005"
        },
        {
          dateOffsetDays: 45,
          branch: "Johannesburg",
          type: "Guest",
          timestampOffsetHours: 240,
          markedBy: "admin003"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Mpho!"],
        attendanceSummary: {
          totalAttendances: 20,
          last90Days: 6,
          raCount: 0
        }
      }
    },
    {
      id: "mem006",
      names: ["Nthabiseng"],
      surname: "Zulu",
      cardNumber: "receipt-006",
      dob: new Date("2000-12-03"), // 23 years old (13-35 range)
      virginityStatus: "non-virgin", // Not Evangelist/Holy Messenger
      dateOfEntry: new Date("2023-03-12"),
      reasonOfEntry: "Spiritual awakening",
      gender: "female",
      position: "member",
      mainBranch: "Johannesburg",
      phone: "+27823456706",
      email: "n.zulu@church.org",
      otpPreference: "email",
      password: "hashedPassword6",
      role: "member",
      lastSeenOffsetDays: 8,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 8,
          branch: "Johannesburg",
          type: "Regular",
          timestampOffsetHours: 160,
          markedBy: "admin006"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Nthabiseng!"],
        attendanceSummary: {
          totalAttendances: 10,
          last90Days: 4,
          raCount: 0
        }
      }
    },
    {
      id: "mem007",
      names: ["Sipho"],
      surname: "Mthembu",
      cardNumber: 1007,
      dob: new Date("1968-06-25"), // 55 years old
      virginityStatus: "inapplicable", // Over 35
      dateOfEntry: new Date("2018-07-01"),
      reasonOfEntry: "Deliverance from witchcraft",
      gender: "male",
      position: "Conciliator",
      mainBranch: "Bulawayo",
      phone: "+27823456707",
      email: "sipho.mthembu@church.org",
      otpPreference: "phone",
      password: "hashedPassword7",
      role: "member",
      lastSeenOffsetDays: 60,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 60,
          branch: "Bulawayo",
          type: "Regular",
          timestampOffsetHours: 360,
          markedBy: "admin007"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Sipho!"],
        attendanceSummary: {
          totalAttendances: 25,
          last90Days: 8,
          raCount: 0
        }
      }
    },
    {
      id: "mem008",
      names: ["Refilwe"],
      surname: "Masango",
      cardNumber: 1008,
      dob: new Date("2005-03-19"), // 18 years old (13-35 range)
      virginityStatus: "virgin", // Not Evangelist/Holy Messenger
      dateOfEntry: new Date("2021-02-28"),
      reasonOfEntry: "Spiritual guidance",
      gender: "female",
      position: "Steward",
      mainBranch: "Gaborone",
      phone: "+27823456708",
      email: "refilwe.masango@church.org",
      otpPreference: "email",
      password: "hashedPassword8",
      role: "member",
      lastSeenOffsetDays: 18,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 18,
          branch: "Gaborone",
          type: "Regular",
          timestampOffsetHours: 220,
          markedBy: "admin008"
        },
        {
          dateOffsetDays: 25,
          branch: "Ireland",
          type: "Guest",
          timestampOffsetHours: 180,
          markedBy: "admin005"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Refilwe!"],
        attendanceSummary: {
          totalAttendances: 16,
          last90Days: 5,
          raCount: 0
        }
      }
    },
    {
      id: "mem009",
      names: ["Tumelo"],
      surname: "Nkosi",
      cardNumber: 1009,
      dob: new Date("2001-10-11"), // 22 years old (13-35 range)
      virginityStatus: "non-virgin", // Not Evangelist/Holy Messenger
      dateOfEntry: new Date("2022-09-05"),
      reasonOfEntry: "Called to spiritual music ministry",
      gender: "male",
      position: "Songster",
      mainBranch: "Harare",
      phone: "+27823456709",
      email: "tumelo.nkosi@church.org",
      otpPreference: "phone",
      password: "hashedPassword9",
      role: "member",
      lastSeenOffsetDays: 25,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 25,
          branch: "Harare",
          type: "Regular",
          timestampOffsetHours: 280,
          markedBy: "admin009"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Tumelo!"],
        attendanceSummary: {
          totalAttendances: 19,
          last90Days: 6,
          raCount: 0
        }
      }
    },
    {
      id: "mem010",
      names: ["Aniyothabo"],
      surname: "Sibanda",
      cardNumber: "1391",
      dob: new Date("1993-03-29"), 
      virginityStatus: "inapplicable", 
      dateOfEntry: new Date("2023-06-20"),
      reasonOfEntry: "Dangerous thoughts",
      gender: "male",
      position: "Member",
      mainBranch: "Johannesburg",
      phone: "+27615019290",
      email: "yothabo.sibanda@church.org",
      otpPreference: "email",
      password: "hashedPassword10",
      role: "member",
      lastSeenOffsetDays: 7,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 7,
          branch: "Ireland",
          type: "Regular",
          timestampOffsetHours: 140,
          markedBy: "admin010"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Thabo!"],
        attendanceSummary: {
          totalAttendances: 8,
          last90Days: 3,
          raCount: 0
        }
      }
    },
    {
      id: "mem011",
      names: ["Kabelo"],
      surname: "Mhlongo",
      cardNumber: 1011,
      dob: new Date("1993-08-15"), // 30 years old (13-35 range)
      virginityStatus: "non-virgin", // Not Evangelist/Holy Messenger
      dateOfEntry: new Date("2020-12-01"),
      reasonOfEntry: "Spiritual transformation",
      gender: "male",
      position: "Member",
      mainBranch: "Bulawayo",
      phone: "+27823456711",
      email: "kabelo.mhlongo@church.org",
      otpPreference: "phone",
      password: "hashedPassword11",
      role: "member",
      lastSeenOffsetDays: 40,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 40,
          branch: "Bulawayo",
          type: "Regular",
          timestampOffsetHours: 320,
          markedBy: "admin011"
        },
        {
          dateOffsetDays: 50,
          branch: "Gaborone",
          type: "Guest",
          timestampOffsetHours: 280,
          markedBy: "admin008"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Kabelo!"],
        attendanceSummary: {
          totalAttendances: 17,
          last90Days: 5,
          raCount: 0
        }
      }
    },
    {
      id: "mem012",
      names: ["Zanele"],
      surname: "Msimang",
      cardNumber: 1012,
      dob: new Date("1965-01-27"), // 58 years old
      virginityStatus: "inapplicable", // Over 35
      dateOfEntry: new Date("2021-04-18"),
      reasonOfEntry: "Healing from spiritual infirmities",
      gender: "female",
      position: "Conciliator",
      mainBranch: "Johannesburg",
      phone: "+27823456712",
      email: "zanele.msimang@church.org",
      otpPreference: "email",
      password: "hashedPassword12",
      role: "super_admin",
      lastSeenOffsetDays: 22,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 22,
          branch: "Johannesburg",
          type: "Regular",
          timestampOffsetHours: 260,
          markedBy: "admin012"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Zanele!"],
        attendanceSummary: {
          totalAttendances: 13,
          last90Days: 4,
          raCount: 0
        }
      }
    },
    {
      id: "mem013",
      names: ["Mandla"],
      surname: "Zwane",
      cardNumber: 1013,
      dob: new Date("1990-05-15"), // 33 years old
      virginityStatus: "non-virgin",
      dateOfEntry: new Date("2019-02-10"),
      reasonOfEntry: "Deliverance from addiction",
      gender: "male",
      position: "Member",
      mainBranch: "Johannesburg",
      phone: "+27823456713",
      email: "mandla.zwane@church.org",
      otpPreference: "phone",
      password: "hashedPassword13",
      role: "member",
      lastSeenOffsetDays: 3,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 3,
          branch: "Johannesburg",
          type: "Regular",
          timestampOffsetHours: 120,
          markedBy: "admin001"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Mandla!"],
        attendanceSummary: {
          totalAttendances: 45,
          last90Days: 12,
          raCount: 0
        }
      }
    },
    {
      id: "mem014",
      names: ["Nomsa"],
      surname: "Xulu",
      cardNumber: 1014,
      dob: new Date("1988-07-22"), // 35 years old
      virginityStatus: "non-virgin",
      dateOfEntry: new Date("2020-11-05"),
      reasonOfEntry: "Spiritual healing from trauma",
      gender: "female",
      position: "Holy Messenger",
      mainBranch: "Bulawayo",
      phone: "+27823456714",
      email: "nomsa.xulu@church.org",
      otpPreference: "email",
      password: "hashedPassword14",
      role: "member",
      lastSeenOffsetDays: 7,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 7,
          branch: "Bulawayo",
          type: "Regular",
          timestampOffsetHours: 150,
          markedBy: "admin002"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Nomsa!"],
        attendanceSummary: {
          totalAttendances: 32,
          last90Days: 9,
          raCount: 0
        }
      }
    },
    {
      id: "mem015",
      names: ["Sibusiso"],
      surname: "Ngwenya",
      cardNumber: 1015,
      dob: new Date("1972-03-18"), // 51 years old
      virginityStatus: "inapplicable",
      dateOfEntry: new Date("2018-09-12"),
      reasonOfEntry: "Freedom from occult practices",
      gender: "male",
      position: "Conciliator",
      mainBranch: "Gaborone",
      phone: "+27823456715",
      email: "sibusiso.ngwenya@church.org",
      otpPreference: "phone",
      password: "hashedPassword15",
      role: "admin",
      lastSeenOffsetDays: 10,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 10,
          branch: "Gaborone",
          type: "Regular",
          timestampOffsetHours: 180,
          markedBy: "admin003"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Sibusiso!"],
        attendanceSummary: {
          totalAttendances: 62,
          last90Days: 15,
          raCount: 0
        }
      }
    },
    {
      id: "mem016",
      names: ["Thandiwe"],
      surname: "Mbele",
      cardNumber: 1016,
      dob: new Date("2007-08-30"), // 16 years old
      virginityStatus: "virgin",
      dateOfEntry: new Date("2023-01-15"),
      reasonOfEntry: "Youth spiritual development",
      gender: "female",
      position: "Member",
      mainBranch: "Harare",
      phone: "+27823456716",
      email: "thandiwe.mbele@church.org",
      otpPreference: "email",
      password: "hashedPassword16",
      role: "member",
      lastSeenOffsetDays: 2,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 2,
          branch: "Harare",
          type: "Regular",
          timestampOffsetHours: 100,
          markedBy: "admin004"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Thandiwe!"],
        attendanceSummary: {
          totalAttendances: 8,
          last90Days: 6,
          raCount: 0
        }
      }
    },
    {
      id: "mem017",
      names: ["Vusi"],
      surname: "Dlamini",
      cardNumber: 1017,
      dob: new Date("1985-11-25"), // 38 years old
      virginityStatus: "inapplicable",
      dateOfEntry: new Date("2017-05-20"),
      reasonOfEntry: "Marriage restoration",
      gender: "male",
      position: "Facilitator",
      mainBranch: "Ireland",
      phone: "+27823456717",
      email: "vusi.dlamini@church.org",
      otpPreference: "phone",
      password: "hashedPassword17",
      role: "member",
      lastSeenOffsetDays: 14,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 14,
          branch: "Ireland",
          type: "Regular",
          timestampOffsetHours: 200,
          markedBy: "admin005"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Vusi!"],
        attendanceSummary: {
          totalAttendances: 28,
          last90Days: 10,
          raCount: 0
        }
      }
    },
    {
      id: "mem018",
      names: ["Nolwazi"],
      surname: "Khumalo",
      cardNumber: 1018,
      dob: new Date("1997-04-12"), // 26 years old
      virginityStatus: "virgin",
      dateOfEntry: new Date("2021-08-03"),
      reasonOfEntry: "Spiritual guidance",
      gender: "female",
      position: "Songster",
      mainBranch: "Johannesburg",
      phone: "+27823456718",
      email: "nolwazi.khumalo@church.org",
      otpPreference: "email",
      password: "hashedPassword18",
      role: "member",
      lastSeenOffsetDays: 5,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 5,
          branch: "Johannesburg",
          type: "Regular",
          timestampOffsetHours: 140,
          markedBy: "admin006"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Nolwazi!"],
        attendanceSummary: {
          totalAttendances: 18,
          last90Days: 7,
          raCount: 0
        }
      }
    },
    {
      id: "mem019",
      names: ["Bongani"],
      surname: "Mthembu",
      cardNumber: 1019,
      dob: new Date("1978-09-08"), // 45 years old
      virginityStatus: "inapplicable",
      dateOfEntry: new Date("2015-12-10"),
      reasonOfEntry: "Deliverance from alcoholism",
      gender: "male",
      position: "Evangelist",
      mainBranch: "Bulawayo",
      phone: "+27823456719",
      email: "bongani.mthembu@church.org",
      otpPreference: "phone",
      password: "hashedPassword19",
      role: "admin",
      lastSeenOffsetDays: 21,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 21,
          branch: "Bulawayo",
          type: "Regular",
          timestampOffsetHours: 240,
          markedBy: "admin007"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Bongani!"],
        attendanceSummary: {
          totalAttendances: 75,
          last90Days: 18,
          raCount: 0
        }
      }
    },
    {
      id: "mem020",
      names: ["Zinhle"],
      surname: "Ndlovu",
      cardNumber: 1020,
      dob: new Date("2004-02-14"), // 19 years old
      virginityStatus: "virgin",
      dateOfEntry: new Date("2022-07-22"),
      reasonOfEntry: "Answered altar call",
      gender: "female",
      position: "Member",
      mainBranch: "Gaborone",
      phone: "+27823456720",
      email: "zinhle.ndlovu@church.org",
      otpPreference: "email",
      password: "hashedPassword20",
      role: "member",
      lastSeenOffsetDays: 1,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 1,
          branch: "Gaborone",
          type: "Regular",
          timestampOffsetHours: 90,
          markedBy: "admin008"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Zinhle!"],
        attendanceSummary: {
          totalAttendances: 14,
          last90Days: 8,
          raCount: 0
        }
      }
    },
    {
      id: "mem021",
      names: ["Sipho"],
      surname: "Mkhize",
      cardNumber: 1021,
      dob: new Date("1963-06-30"), // 60 years old
      virginityStatus: "inapplicable",
      dateOfEntry: new Date("2014-04-05"),
      reasonOfEntry: "Healing from terminal illness",
      gender: "male",
      position: "Conciliator",
      mainBranch: "Harare",
      phone: "+27823456721",
      email: "sipho.mkhize@church.org",
      otpPreference: "phone",
      password: "hashedPassword21",
      role: "member",
      lastSeenOffsetDays: 28,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 28,
          branch: "Harare",
          type: "Regular",
          timestampOffsetHours: 300,
          markedBy: "admin009"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Sipho!"],
        attendanceSummary: {
          totalAttendances: 68,
          last90Days: 12,
          raCount: 0
        }
      }
    },
    {
      id: "mem022",
      names: ["Nomvula"],
      surname: "Zulu",
      cardNumber: 1022,
      dob: new Date("1992-12-05"), // 31 years old
      virginityStatus: "non-virgin",
      dateOfEntry: new Date("2019-10-15"),
      reasonOfEntry: "Marriage restoration",
      gender: "female",
      position: "Steward",
      mainBranch: "Ireland",
      phone: "+27823456722",
      email: "nomvula.zulu@church.org",
      otpPreference: "email",
      password: "hashedPassword22",
      role: "member",
      lastSeenOffsetDays: 9,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 9,
          branch: "Ireland",
          type: "Regular",
          timestampOffsetHours: 160,
          markedBy: "admin010"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Nomvula!"],
        attendanceSummary: {
          totalAttendances: 24,
          last90Days: 9,
          raCount: 0
        }
      }
    },
    {
      id: "mem023",
      names: ["Themba"],
      surname: "Sithole",
      cardNumber: 1023,
      dob: new Date("2009-07-18"), // 14 years old
      virginityStatus: "virgin",
      dateOfEntry: new Date("2023-02-10"),
      reasonOfEntry: "Under parental guidance",
      gender: "male",
      position: "Member",
      mainBranch: "Johannesburg",
      phone: "+27823456723",
      email: "themba.sithole@church.org",
      otpPreference: "phone",
      password: "hashedPassword23",
      role: "member",
      lastSeenOffsetDays: 4,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 4,
          branch: "Johannesburg",
          type: "Regular",
          timestampOffsetHours: 110,
          markedBy: "admin011"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Themba!"],
        attendanceSummary: {
          totalAttendances: 6,
          last90Days: 5,
          raCount: 0
        }
      }
    },
    {
      id: "mem024",
      names: ["Lindiwe"],
      surname: "Mabaso",
      cardNumber: 1024,
      dob: new Date("1980-01-20"), // 43 years old
      virginityStatus: "inapplicable",
      dateOfEntry: new Date("2016-08-12"),
      reasonOfEntry: "Deliverance from depression",
      gender: "female",
      position: "Facilitator",
      mainBranch: "Bulawayo",
      phone: "+27823456724",
      email: "lindiwe.mabaso@church.org",
      otpPreference: "email",
      password: "hashedPassword24",
      role: "admin",
      lastSeenOffsetDays: 17,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 17,
          branch: "Bulawayo",
          type: "Regular",
          timestampOffsetHours: 220,
          markedBy: "admin012"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Lindiwe!"],
        attendanceSummary: {
          totalAttendances: 52,
          last90Days: 14,
          raCount: 0
        }
      }
    },
    {
      id: "mem025",
      names: ["Sandile"],
      surname: "Mthethwa",
      cardNumber: 1025,
      dob: new Date("1994-09-15"), // 29 years old (13-35 range)
      virginityStatus: "non-virgin",
      dateOfEntry: new Date("2020-05-18"),
      reasonOfEntry: "Deliverance from gambling addiction",
      gender: "male",
      position: "Member",
      mainBranch: "Gaborone",
      phone: "+27823456725",
      email: "sandile.mthethwa@church.org",
      otpPreference: "phone",
      password: "hashedPassword25",
      role: "member",
      lastSeenOffsetDays: 95, // RA status
      raCount: 95,
      status: "RA",
      reAdmissionOffsetDays: 95,
      raHistory: [{
        dateOffsetDays: 95,
        reason: "Extended absence",
        removedDate: null,
        removedReason: null
      }],
      trackingActive: true,
      preRaWarningSentOffsetDays: 85,
      globalLock: true,
      lockStatus: "RA Lock",
      attendance: [
        {
          dateOffsetDays: 95,
          branch: "Gaborone",
          type: "Regular",
          timestampOffsetHours: 240,
          markedBy: "admin001"
        }
      ],
      selfManagementData: {
        messages: ["You have been placed on RA due to extended absence"],
        attendanceSummary: {
          totalAttendances: 42,
          last90Days: 0,
          raCount: 95
        }
      }
    },
    {
      id: "mem026",
      names: ["Nokuthula"],
      surname: "Nkosi",
      cardNumber: 1026,
      dob: new Date("1987-12-22"), // 36 years old
      virginityStatus: "inapplicable", // Over 35
      dateOfEntry: new Date("2018-11-07"),
      reasonOfEntry: "Healing from chronic illness",
      gender: "female",
      position: "Holy Messenger",
      mainBranch: "Harare",
      phone: "+27823456726",
      email: "nokuthula.nkosi@church.org",
      otpPreference: "email",
      password: "hashedPassword26",
      role: "member",
      lastSeenOffsetDays: 87, // Pre-RA status
      raCount: 87,
      status: "Pre-RA",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: 85,
      globalLock: false,
      lockStatus: "Warning",
      attendance: [
        {
          dateOffsetDays: 87,
          branch: "Harare",
          type: "Regular",
          timestampOffsetHours: 210,
          markedBy: "admin002"
        }
      ],
      selfManagementData: {
        messages: ["Warning: You're approaching RA status"],
        attendanceSummary: {
          totalAttendances: 58,
          last90Days: 3,
          raCount: 87
        }
      }
    },
    {
      id: "mem027",
      names: ["Bheki"],
      surname: "Mdluli",
      cardNumber: 1027,
      dob: new Date("1975-04-30"), // 48 years old
      virginityStatus: "inapplicable",
      dateOfEntry: new Date("2015-07-12"),
      reasonOfEntry: "Freedom from ancestral curses",
      gender: "male",
      position: "Conciliator",
      mainBranch: "Ireland",
      phone: "+27823456727",
      email: "bheki.mdluli@church.org",
      otpPreference: "phone",
      password: "hashedPassword27",
      role: "admin",
      lastSeenOffsetDays: 92, // RA status
      raCount: 92,
      status: "RA",
      reAdmissionOffsetDays: 92,
      raHistory: [{
        dateOffsetDays: 92,
        reason: "Extended travel",
        removedDate: null,
        removedReason: null
      }],
      trackingActive: true,
      preRaWarningSentOffsetDays: 85,
      globalLock: true,
      lockStatus: "RA Lock",
      attendance: [
        {
          dateOffsetDays: 92,
          branch: "Ireland",
          type: "Regular",
          timestampOffsetHours: 280,
          markedBy: "admin003"
        }
      ],
      selfManagementData: {
        messages: ["You have been placed on RA status"],
        attendanceSummary: {
          totalAttendances: 73,
          last90Days: 1,
          raCount: 92
        }
      }
    },
    {
      id: "mem028",
      names: ["Thulisile"],
      surname: "Zwane",
      cardNumber: 1028,
      dob: new Date("2002-06-18"), // 21 years old
      virginityStatus: "virgin",
      dateOfEntry: new Date("2022-03-05"),
      reasonOfEntry: "Spiritual calling",
      gender: "female",
      position: "Member",
      mainBranch: "Johannesburg",
      phone: "+27823456728",
      email: "thulisile.zwane@church.org",
      otpPreference: "email",
      password: "hashedPassword28",
      role: "member",
      lastSeenOffsetDays: 88, // Pre-RA status
      raCount: 88,
      status: "Pre-RA",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: 85,
      globalLock: false,
      lockStatus: "Warning",
      attendance: [
        {
          dateOffsetDays: 88,
          branch: "Johannesburg",
          type: "Regular",
          timestampOffsetHours: 190,
          markedBy: "admin004"
        }
      ],
      selfManagementData: {
        messages: ["Warning: Approaching RA threshold"],
        attendanceSummary: {
          totalAttendances: 19,
          last90Days: 2,
          raCount: 88
        }
      }
    },
    {
      id: "mem029",
      names: ["Sifiso"],
      surname: "Ndlovu",
      cardNumber: 1029,
      dob: new Date("1991-08-25"), // 32 years old
      virginityStatus: "non-virgin",
      dateOfEntry: new Date("2019-09-14"),
      reasonOfEntry: "Marriage restoration",
      gender: "male",
      position: "Steward",
      mainBranch: "Bulawayo",
      phone: "+27823456729",
      email: "sifiso.ndlovu@church.org",
      otpPreference: "phone",
      password: "hashedPassword29",
      role: "member",
      lastSeenOffsetDays: 91, // RA status
      raCount: 91,
      status: "RA",
      reAdmissionOffsetDays: 91,
      raHistory: [{
        dateOffsetDays: 91,
        reason: "Work commitments",
        removedDate: null,
        removedReason: null
      }],
      trackingActive: true,
      preRaWarningSentOffsetDays: 85,
      globalLock: true,
      lockStatus: "RA Lock",
      attendance: [
        {
          dateOffsetDays: 91,
          branch: "Bulawayo",
          type: "Regular",
          timestampOffsetHours: 260,
          markedBy: "admin005"
        }
      ],
      selfManagementData: {
        messages: ["You have been placed on RA status"],
        attendanceSummary: {
          totalAttendances: 37,
          last90Days: 0,
          raCount: 91
        }
      }
    },
    {
      id: "mem030",
      names: ["Nompumelelo"],
      surname: "Mkhwanazi",
      cardNumber: 1030,
      dob: new Date("1983-11-12"), // 40 years old
      virginityStatus: "inapplicable",
      dateOfEntry: new Date("2017-02-28"),
      reasonOfEntry: "Healing from infertility",
      gender: "female",
      position: "Holy Messenger",
      mainBranch: "Gaborone",
      phone: "+27823456730",
      email: "nompumelelo.mkhwanazi@church.org",
      otpPreference: "email",
      password: "hashedPassword30",
      role: "member",
      lastSeenOffsetDays: 86, // Pre-RA status
      raCount: 86,
      status: "Pre-RA",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: 85,
      globalLock: false,
      lockStatus: "Warning",
      attendance: [
        {
          dateOffsetDays: 86,
          branch: "Gaborone",
          type: "Regular",
          timestampOffsetHours: 230,
          markedBy: "admin006"
        }
      ],
      selfManagementData: {
        messages: ["Warning: Approaching RA threshold"],
        attendanceSummary: {
          totalAttendances: 49,
          last90Days: 4,
          raCount: 86
        }
      }
    },
    {
      id: "mem031",
      names: ["Jabulani"],
      surname: "Mthembu",
      cardNumber: 1031,
      dob: new Date("2005-03-08"), // 18 years old
      virginityStatus: "virgin",
      dateOfEntry: new Date("2023-04-15"),
      reasonOfEntry: "Youth spiritual development",
      gender: "male",
      position: "Member",
      mainBranch: "Harare",
      phone: "+27823456731",
      email: "jabulani.mthembu@church.org",
      otpPreference: "phone",
      password: "hashedPassword31",
      role: "member",
      lastSeenOffsetDays: 93, // RA status
      raCount: 93,
      status: "RA",
      reAdmissionOffsetDays: 93,
      raHistory: [{
        dateOffsetDays: 93,
        reason: "University studies",
        removedDate: null,
        removedReason: null
      }],
      trackingActive: true,
      preRaWarningSentOffsetDays: 85,
      globalLock: true,
      lockStatus: "RA Lock",
      attendance: [
        {
          dateOffsetDays: 93,
          branch: "Harare",
          type: "Regular",
          timestampOffsetHours: 270,
          markedBy: "admin007"
        }
      ],
      selfManagementData: {
        messages: ["You have been placed on RA status"],
        attendanceSummary: {
          totalAttendances: 7,
          last90Days: 0,
          raCount: 93
        }
      }
    },
    {
      id: "mem032",
      names: ["Ntombifuthi"],
      surname: "Zungu",
      cardNumber: 1032,
      dob: new Date("1996-07-22"), // 27 years old
      virginityStatus: "non-virgin",
      dateOfEntry: new Date("2020-12-10"),
      reasonOfEntry: "Deliverance from depression",
      gender: "female",
      position: "Songster",
      mainBranch: "Ireland",
      phone: "+27823456732",
      email: "ntombifuthi.zungu@church.org",
      otpPreference: "email",
      password: "hashedPassword32",
      role: "member",
      lastSeenOffsetDays: 89, // Pre-RA status
      raCount: 89,
      status: "Pre-RA",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: 85,
      globalLock: false,
      lockStatus: "Warning",
      attendance: [
        {
          dateOffsetDays: 89,
          branch: "Ireland",
          type: "Regular",
          timestampOffsetHours: 250,
          markedBy: "admin008"
        }
      ],
      selfManagementData: {
        messages: ["Warning: Approaching RA threshold"],
        attendanceSummary: {
          totalAttendances: 31,
          last90Days: 1,
          raCount: 89
        }
      }
    },
    {
      id: "mem033",
      names: ["Mxolisi"],
      surname: "Ntuli",
      cardNumber: 1033,
      dob: new Date("1979-05-14"), // 44 years old
      virginityStatus: "inapplicable",
      dateOfEntry: new Date("2016-10-05"),
      reasonOfEntry: "Freedom from financial curses",
      gender: "male",
      position: "Evangelist",
      mainBranch: "Johannesburg",
      phone: "+27823456733",
      email: "mxolisi.ntuli@church.org",
      otpPreference: "phone",
      password: "hashedPassword33",
      role: "admin",
      lastSeenOffsetDays: 94, // RA status
      raCount: 94,
      status: "RA",
      reAdmissionOffsetDays: 94,
      raHistory: [{
        dateOffsetDays: 94,
        reason: "Missionary work abroad",
        removedDate: null,
        removedReason: null
      }],
      trackingActive: true,
      preRaWarningSentOffsetDays: 85,
      globalLock: true,
      lockStatus: "RA Lock",
      attendance: [
        {
          dateOffsetDays: 94,
          branch: "Johannesburg",
          type: "Regular",
          timestampOffsetHours: 290,
          markedBy: "admin009"
        }
      ],
      selfManagementData: {
        messages: ["You have been placed on RA status"],
        attendanceSummary: {
          totalAttendances: 65,
          last90Days: 0,
          raCount: 94
        }
      }
    },
    {
      id: "mem034",
      names: ["Zodwa"],
      surname: "Mbatha",
      cardNumber: 1034,
      dob: new Date("1988-02-28"), // 35 years old
      virginityStatus: "non-virgin",
      dateOfEntry: new Date("2018-08-17"),
      reasonOfEntry: "Healing from autoimmune disease",
      gender: "female",
      position: "Facilitator",
      mainBranch: "Bulawayo",
      phone: "+27823456734",
      email: "zodwa.mbatha@church.org",
      otpPreference: "email",
      password: "hashedPassword34",
      role: "member",
      lastSeenOffsetDays: 87, // Pre-RA status
      raCount: 87,
      status: "Pre-RA",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: 85,
      globalLock: false,
      lockStatus: "Warning",
      attendance: [
        {
          dateOffsetDays: 87,
          branch: "Bulawayo",
          type: "Regular",
          timestampOffsetHours: 220,
          markedBy: "admin010"
        }
      ],
      selfManagementData: {
        messages: ["Warning: Approaching RA threshold"],
        attendanceSummary: {
          totalAttendances: 43,
          last90Days: 3,
          raCount: 87
        }
      }
    },
    {
      id: "mem035",
      names: ["Sibusiso"],
      surname: "Mhlongo",
      cardNumber: 1035,
      dob: new Date("2008-09-10"), // 15 years old
      virginityStatus: "virgin",
      dateOfEntry: new Date("2023-05-22"),
      reasonOfEntry: "Under parental guidance",
      gender: "male",
      position: "Member",
      mainBranch: "Gaborone",
      phone: "+27823456735",
      email: "sibusiso.mhlongo@church.org",
      otpPreference: "phone",
      password: "hashedPassword35",
      role: "member",
      lastSeenOffsetDays: 90, // RA status
      raCount: 90,
      status: "RA",
      reAdmissionOffsetDays: 90,
      raHistory: [{
        dateOffsetDays: 90,
        reason: "Boarding school",
        removedDate: null,
        removedReason: null
      }],
      trackingActive: true,
      preRaWarningSentOffsetDays: 85,
      globalLock: true,
      lockStatus: "RA Lock",
      attendance: [
        {
          dateOffsetDays: 90,
          branch: "Gaborone",
          type: "Regular",
          timestampOffsetHours: 200,
          markedBy: "admin011"
        }
      ],
      selfManagementData: {
        messages: ["You have been placed on RA status"],
        attendanceSummary: {
          totalAttendances: 5,
          last90Days: 0,
          raCount: 90
        }
      }
    },
    {
      id: "mem036",
      names: ["Noluthando"],
      surname: "Dlamini",
      cardNumber: 1036,
      dob: new Date("1993-12-05"), // 30 years old
      virginityStatus: "non-virgin",
      dateOfEntry: new Date("2019-06-30"),
      reasonOfEntry: "Spiritual guidance",
      gender: "female",
      position: "Holy Messenger",
      mainBranch: "Harare",
      phone: "+27823456736",
      email: "noluthando.dlamini@church.org",
      otpPreference: "email",
      password: "hashedPassword36",
      role: "member",
      lastSeenOffsetDays: 86, // Pre-RA status
      raCount: 86,
      status: "Pre-RA",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: 85,
      globalLock: false,
      lockStatus: "Warning",
      attendance: [
        {
          dateOffsetDays: 86,
          branch: "Harare",
          type: "Regular",
          timestampOffsetHours: 210,
          markedBy: "admin012"
        }
      ],
      selfManagementData: {
        messages: ["Warning: Approaching RA threshold"],
        attendanceSummary: {
          totalAttendances: 28,
          last90Days: 4,
          raCount: 86
        }
      }
    },
    {
      id: "mem037",
      names: ["Thando"],
      surname: "Mkhize",
      cardNumber: 1037,
      dob: new Date("1995-04-12"), // 28 years old
      virginityStatus: "non-virgin",
      dateOfEntry: new Date("2019-03-15"),
      reasonOfEntry: "Deliverance from substance abuse",
      gender: "female",
      position: "Member",
      mainBranch: "Johannesburg",
      phone: "+27823456737",
      email: "thando.mkhize@church.org",
      otpPreference: "phone",
      password: "hashedPassword37",
      role: "member",
      lastSeenOffsetDays: 5,
      raCount: 4, // Exceeds 3 RAs - no longer tracked
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [
        { dateOffsetDays: 210, reason: "Work relocation", removedDate: "2023-01-15", removedReason: "Returned to area" },
        { dateOffsetDays: 150, reason: "Further studies", removedDate: "2022-08-20", removedReason: "Studies completed" },
        { dateOffsetDays: 95, reason: "Medical treatment", removedDate: "2022-03-10", removedReason: "Recovery" }
      ],
      trackingActive: false,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Untracked",
      attendance: [
        {
          dateOffsetDays: 5,
          branch: "Johannesburg",
          type: "Regular",
          timestampOffsetHours: 120,
          markedBy: "admin001"
        }
      ],
      selfManagementData: {
        messages: ["RA tracking suspended due to multiple RA instances"],
        attendanceSummary: {
          totalAttendances: 48,
          last90Days: 12,
          raCount: 4
        }
      }
    },
    {
      id: "mem038",
      names: ["Sipho"],
      surname: "Ndlovu",
      cardNumber: 1038,
      dob: new Date("1978-11-30"), // 45 years old
      virginityStatus: "inapplicable",
      dateOfEntry: new Date("2015-07-22"),
      reasonOfEntry: "Healing from chronic pain",
      gender: "male",
      position: "Conciliator",
      mainBranch: "Bulawayo",
      phone: "+27823456738",
      email: "sipho.ndlovu@church.org",
      otpPreference: "email",
      password: "hashedPassword38",
      role: "admin",
      lastSeenOffsetDays: 92, // Currently in RA
      raCount: 2,
      status: "RA",
      reAdmissionOffsetDays: 92,
      raHistory: [
        { dateOffsetDays: 180, reason: "Missionary work", removedDate: "2022-11-15", removedReason: "Mission completed" },
        { dateOffsetDays: 92, reason: "Elderly care", removedDate: null, removedReason: null }
      ],
      trackingActive: true,
      preRaWarningSentOffsetDays: 85,
      globalLock: true,
      lockStatus: "RA Lock",
      attendance: [
        {
          dateOffsetDays: 92,
          branch: "Bulawayo",
          type: "Regular",
          timestampOffsetHours: 240,
          markedBy: "admin002"
        }
      ],
      selfManagementData: {
        messages: ["You have been placed on RA status for elderly care"],
        attendanceSummary: {
          totalAttendances: 72,
          last90Days: 1,
          raCount: 2
        }
      }
    },
    {
      id: "mem039",
      names: ["Noluthando"],
      surname: "Zwane",
      cardNumber: 1039,
      dob: new Date("2001-02-14"), // 22 years old
      virginityStatus: "virgin",
      dateOfEntry: new Date("2021-09-05"),
      reasonOfEntry: "Spiritual guidance",
      gender: "female",
      position: "Songster",
      mainBranch: "Gaborone",
      phone: "+27823456739",
      email: "noluthando.zwane@church.org",
      otpPreference: "phone",
      password: "hashedPassword39",
      role: "member",
      lastSeenOffsetDays: 87, // Pre-RA status
      raCount: 1,
      status: "Pre-RA",
      reAdmissionOffsetDays: null,
      raHistory: [
        { dateOffsetDays: 87, reason: "University exchange", removedDate: null, removedReason: null }
      ],
      trackingActive: true,
      preRaWarningSentOffsetDays: 85,
      globalLock: false,
      lockStatus: "Warning",
      attendance: [
        {
          dateOffsetDays: 87,
          branch: "Gaborone",
          type: "Regular",
          timestampOffsetHours: 200,
          markedBy: "admin003"
        }
      ],
      selfManagementData: {
        messages: ["Warning: Approaching RA threshold due to university exchange"],
        attendanceSummary: {
          totalAttendances: 25,
          last90Days: 3,
          raCount: 1
        }
      }
    },
    {
      id: "mem040",
      names: ["Mandla"],
      surname: "Mthembu",
      cardNumber: 1040,
      dob: new Date("1989-06-18"), // 34 years old
      virginityStatus: "non-virgin",
      dateOfEntry: new Date("2018-04-12"),
      reasonOfEntry: "Marriage restoration",
      gender: "male",
      position: "Steward",
      mainBranch: "Harare",
      phone: "+27823456740",
      email: "mandla.mthembu@church.org",
      otpPreference: "email",
      password: "hashedPassword40",
      role: "member",
      lastSeenOffsetDays: 4,
      raCount: 5, // Exceeds 3 RAs - no longer tracked
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [
        { dateOffsetDays: 300, reason: "Work contract abroad", removedDate: "2022-10-15", removedReason: "Contract ended" },
        { dateOffsetDays: 210, reason: "Family crisis", removedDate: "2022-03-20", removedReason: "Situation resolved" },
        { dateOffsetDays: 150, reason: "Further studies", removedDate: "2021-10-05", removedReason: "Graduated" },
        { dateOffsetDays: 90, reason: "Medical treatment", removedDate: "2021-04-15", removedReason: "Recovery" },
        { dateOffsetDays: 30, reason: "Temporary relocation", removedDate: "2021-01-10", removedReason: "Returned" }
      ],
      trackingActive: false,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Untracked",
      attendance: [
        {
          dateOffsetDays: 4,
          branch: "Harare",
          type: "Regular",
          timestampOffsetHours: 100,
          markedBy: "admin004"
        }
      ],
      selfManagementData: {
        messages: ["RA tracking suspended due to multiple RA instances"],
        attendanceSummary: {
          totalAttendances: 52,
          last90Days: 15,
          raCount: 5
        }
      }
    },
    {
      id: "mem041",
      names: ["Zanele"],
      surname: "Mkhwanazi",
      cardNumber: 1041,
      dob: new Date("2006-09-25"), // 17 years old
      virginityStatus: "virgin",
      dateOfEntry: new Date("2023-02-18"),
      reasonOfEntry: "Under parental guidance",
      gender: "female",
      position: "Member",
      mainBranch: "Ireland",
      phone: "+27823456741",
      email: "zanele.mkhwanazi@church.org",
      otpPreference: "phone",
      password: "hashedPassword41",
      role: "member",
      lastSeenOffsetDays: 91, // RA status
      raCount: 1,
      status: "RA",
      reAdmissionOffsetDays: 91,
      raHistory: [
        { dateOffsetDays: 91, reason: "Boarding school", removedDate: null, removedReason: null }
      ],
      trackingActive: true,
      preRaWarningSentOffsetDays: 85,
      globalLock: true,
      lockStatus: "RA Lock",
      attendance: [
        {
          dateOffsetDays: 91,
          branch: "Ireland",
          type: "Regular",
          timestampOffsetHours: 180,
          markedBy: "admin005"
        }
      ],
      selfManagementData: {
        messages: ["You have been placed on RA status due to boarding school"],
        attendanceSummary: {
          totalAttendances: 8,
          last90Days: 0,
          raCount: 1
        }
      }
    },
    {
      id: "mem042",
      names: ["Bongani"],
      surname: "Dlamini",
      cardNumber: 1042,
      dob: new Date("1982-12-05"), // 41 years old
      virginityStatus: "inapplicable",
      dateOfEntry: new Date("2016-08-30"),
      reasonOfEntry: "Deliverance from financial curses",
      gender: "male",
      position: "Evangelist",
      mainBranch: "Johannesburg",
      phone: "+27823456742",
      email: "bongani.dlamini@church.org",
      otpPreference: "email",
      password: "hashedPassword42",
      role: "admin",
      lastSeenOffsetDays: 3,
      raCount: 4, // Exceeds 3 RAs - no longer tracked
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [
        { dateOffsetDays: 240, reason: "Missionary work", removedDate: "2022-12-10", removedReason: "Mission completed" },
        { dateOffsetDays: 180, reason: "Family crisis", removedDate: "2022-06-15", removedReason: "Situation resolved" },
        { dateOffsetDays: 120, reason: "Medical treatment", removedDate: "2022-02-20", removedReason: "Recovery" },
        { dateOffsetDays: 60, reason: "Elderly parent care", removedDate: "2021-12-05", removedReason: "Parent passed" }
      ],
      trackingActive: false,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Untracked",
      attendance: [
        {
          dateOffsetDays: 3,
          branch: "Johannesburg",
          type: "Regular",
          timestampOffsetHours: 90,
          markedBy: "admin006"
        }
      ],
      selfManagementData: {
        messages: ["RA tracking suspended due to multiple RA instances"],
        attendanceSummary: {
          totalAttendances: 68,
          last90Days: 18,
          raCount: 4
        }
      }
    },
    {
      id: "mem043",
      names: ["Nomfundo"],
      surname: "Nkosi",
      cardNumber: 1043,
      dob: new Date("1997-03-28"), // 26 years old
      virginityStatus: "non-virgin",
      dateOfEntry: new Date("2020-11-15"),
      reasonOfEntry: "Spiritual awakening",
      gender: "female",
      position: "Holy Messenger",
      mainBranch: "Bulawayo",
      phone: "+27823456743",
      email: "nomfundo.nkosi@church.org",
      otpPreference: "phone",
      password: "hashedPassword43",
      role: "member",
      lastSeenOffsetDays: 88, // Pre-RA status
      raCount: 2,
      status: "Pre-RA",
      reAdmissionOffsetDays: null,
      raHistory: [
        { dateOffsetDays: 150, reason: "Further studies", removedDate: "2022-08-20", removedReason: "Graduated" },
        { dateOffsetDays: 88, reason: "Family relocation", removedDate: null, removedReason: null }
      ],
      trackingActive: true,
      preRaWarningSentOffsetDays: 85,
      globalLock: false,
      lockStatus: "Warning",
      attendance: [
        {
          dateOffsetDays: 88,
          branch: "Bulawayo",
          type: "Regular",
          timestampOffsetHours: 210,
          markedBy: "admin007"
        }
      ],
      selfManagementData: {
        messages: ["Warning: Approaching RA threshold due to family relocation"],
        attendanceSummary: {
          totalAttendances: 32,
          last90Days: 2,
          raCount: 2
        }
      }
    },
    {
      id: "mem044",
      names: ["Thabang"],
      surname: "Molefe",
      cardNumber: 1044,
      dob: new Date("1975-08-12"), // 48 years old
      virginityStatus: "inapplicable",
      dateOfEntry: new Date("2014-05-20"),
      reasonOfEntry: "Freedom from ancestral curses",
      gender: "male",
      position: "Conciliator",
      mainBranch: "Gaborone",
      phone: "+27823456744",
      email: "thabang.molefe@church.org",
      otpPreference: "email",
      password: "hashedPassword44",
      role: "member",
      lastSeenOffsetDays: 95, // RA status
      raCount: 3, // At threshold - still tracked
      status: "RA",
      reAdmissionOffsetDays: 95,
      raHistory: [
        { dateOffsetDays: 200, reason: "Work contract abroad", removedDate: "2022-10-15", removedReason: "Contract ended" },
        { dateOffsetDays: 130, reason: "Medical treatment", removedDate: "2022-04-20", removedReason: "Recovery" },
        { dateOffsetDays: 95, reason: "Elderly care", removedDate: null, removedReason: null }
      ],
      trackingActive: true,
      preRaWarningSentOffsetDays: 85,
      globalLock: true,
      lockStatus: "RA Lock",
      attendance: [
        {
          dateOffsetDays: 95,
          branch: "Gaborone",
          type: "Regular",
          timestampOffsetHours: 250,
          markedBy: "admin008"
        }
      ],
      selfManagementData: {
        messages: ["You have been placed on RA status for elderly care"],
        attendanceSummary: {
          totalAttendances: 58,
          last90Days: 0,
          raCount: 3
        }
      }
    },
    {
      id: "mem045",
      names: ["Nomsa"],
      surname: "Zulu",
      cardNumber: 1045,
      dob: new Date("1986-01-30"), // 37 years old
      virginityStatus: "inapplicable",
      dateOfEntry: new Date("2017-09-12"),
      reasonOfEntry: "Healing from infertility",
      gender: "female",
      position: "Facilitator",
      mainBranch: "Harare",
      phone: "+27823456745",
      email: "nomsa.zulu@church.org",
      otpPreference: "phone",
      password: "hashedPassword45",
      role: "member",
      lastSeenOffsetDays: 6,
      raCount: 4, // Exceeds 3 RAs - no longer tracked
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [
        { dateOffsetDays: 220, reason: "Work relocation", removedDate: "2022-11-20", removedReason: "Returned" },
        { dateOffsetDays: 170, reason: "Family crisis", removedDate: "2022-06-15", removedReason: "Situation resolved" },
        { dateOffsetDays: 110, reason: "Medical treatment", removedDate: "2022-02-10", removedReason: "Recovery" },
        { dateOffsetDays: 50, reason: "Further studies", removedDate: "2021-11-05", removedReason: "Graduated" }
      ],
      trackingActive: false,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Untracked",
      attendance: [
        {
          dateOffsetDays: 6,
          branch: "Harare",
          type: "Regular",
          timestampOffsetHours: 110,
          markedBy: "admin009"
        }
      ],
      selfManagementData: {
        messages: ["RA tracking suspended due to multiple RA instances"],
        attendanceSummary: {
          totalAttendances: 47,
          last90Days: 14,
          raCount: 4
        }
      }
    },
    {
      id: "mem046",
      names: ["Sizwe"],
      surname: "Mabaso",
      cardNumber: 1046,
      dob: new Date("2003-07-15"), // 20 years old
      virginityStatus: "virgin",
      dateOfEntry: new Date("2022-04-05"),
      reasonOfEntry: "Youth spiritual development",
      gender: "male",
      position: "Member",
      mainBranch: "Ireland",
      phone: "+27823456746",
      email: "sizwe.mabaso@church.org",
      otpPreference: "email",
      password: "hashedPassword46",
      role: "member",
      lastSeenOffsetDays: 89, // Pre-RA status
      raCount: 1,
      status: "Pre-RA",
      reAdmissionOffsetDays: null,
      raHistory: [
        { dateOffsetDays: 89, reason: "University exchange", removedDate: null, removedReason: null }
      ],
      trackingActive: true,
      preRaWarningSentOffsetDays: 85,
      globalLock: false,
      lockStatus: "Warning",
      attendance: [
        {
          dateOffsetDays: 89,
          branch: "Ireland",
          type: "Regular",
          timestampOffsetHours: 190,
          markedBy: "admin010"
        }
      ],
      selfManagementData: {
        messages: ["Warning: Approaching RA threshold due to university exchange"],
        attendanceSummary: {
          totalAttendances: 18,
          last90Days: 1,
          raCount: 1
        }
      }
    },
    {
      id: "mem047",
      names: ["Thulisile"],
      surname: "Mthembu",
      cardNumber: 1047,
      dob: new Date("1990-10-22"), // 33 years old
      virginityStatus: "non-virgin",
      dateOfEntry: new Date("2018-12-10"),
      reasonOfEntry: "Marriage restoration",
      gender: "female",
      position: "Steward",
      mainBranch: "Johannesburg",
      phone: "+27823456747",
      email: "thulisile.mthembu@church.org",
      otpPreference: "phone",
      password: "hashedPassword47",
      role: "member",
      lastSeenOffsetDays: 93, // RA status
      raCount: 2,
      status: "RA",
      reAdmissionOffsetDays: 93,
      raHistory: [
        { dateOffsetDays: 160, reason: "Work contract abroad", removedDate: "2022-07-15", removedReason: "Contract ended" },
        { dateOffsetDays: 93, reason: "Intensive illness", removedDate: null, removedReason: null }
      ],
      trackingActive: true,
      preRaWarningSentOffsetDays: 85,
      globalLock: true,
      lockStatus: "RA Lock",
      attendance: [
        {
          dateOffsetDays: 93,
          branch: "Johannesburg",
          type: "Regular",
          timestampOffsetHours: 230,
          markedBy: "admin011"
        }
      ],
      selfManagementData: {
        messages: ["You have been placed on RA status due to intensive illness"],
        attendanceSummary: {
          totalAttendances: 36,
          last90Days: 0,
          raCount: 2
        }
      }
    },
    {
      id: "mem048",
      names: ["Mxolisi"],
      surname: "Ndlovu",
      cardNumber: 1048,
      dob: new Date("1984-05-08"), // 39 years old
      virginityStatus: "inapplicable",
      dateOfEntry: new Date("2016-02-15"),
      reasonOfEntry: "Deliverance from alcoholism",
      gender: "male",
      position: "Evangelist",
      mainBranch: "Bulawayo",
      phone: "+27823456748",
      email: "mxolisi.ndlovu@church.org",
      otpPreference: "email",
      password: "hashedPassword48",
      role: "admin",
      lastSeenOffsetDays: 2,
      raCount: 4, // Exceeds 3 RAs - no longer tracked
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [
        { dateOffsetDays: 240, reason: "Missionary work", removedDate: "2022-12-20", removedReason: "Mission completed" },
        { dateOffsetDays: 180, reason: "Family relocation", removedDate: "2022-06-15", removedReason: "Returned" },
        { dateOffsetDays: 120, reason: "Medical treatment", removedDate: "2022-02-10", removedReason: "Recovery" },
        { dateOffsetDays: 60, reason: "Further studies", removedDate: "2021-11-05", removedReason: "Graduated" }
      ],
      trackingActive: false,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Untracked",
      attendance: [
        {
          dateOffsetDays: 2,
          branch: "Bulawayo",
          type: "Regular",
          timestampOffsetHours: 80,
          markedBy: "admin012"
        }
      ],
      selfManagementData: {
        messages: ["RA tracking suspended due to multiple RA instances"],
        attendanceSummary: {
          totalAttendances: 62,
          last90Days: 16,
          raCount: 4
        }
      }
    },
    {
      id: "mem049",
      names: ["Lungile"],
      surname: "Mthembu",
      cardNumber: 1049,
      dob: new Date("1988-03-15"),
      virginityStatus: "non-virgin",
      dateOfEntry: new Date("2017-06-20"),
      reasonOfEntry: "Deliverance from depression",
      gender: "female",
      position: "Facilitator",
      mainBranch: "Johannesburg",
      phone: "+27823456749",
      email: "lungile.mthembu@church.org",
      otpPreference: "email",
      password: "hashedPassword49",
      role: "member",
      lastSeenOffsetDays: 15,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 15,
          branch: "Johannesburg",
          type: "Regular",
          timestampOffsetHours: 180,
          markedBy: "admin001"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Lungile!"],
        attendanceSummary: {
          totalAttendances: 42,
          last90Days: 12,
          raCount: 0
        }
      }
    },
    {
      id: "mem050",
      names: ["Sipho"],
      surname: "Nkosi",
      cardNumber: 1050,
      dob: new Date("1972-07-22"),
      virginityStatus: "inapplicable",
      dateOfEntry: new Date("2015-11-10"),
      reasonOfEntry: "Healing from cancer",
      gender: "male",
      position: "Conciliator",
      mainBranch: "Bulawayo",
      phone: "+27823456750",
      email: "sipho.nkosi@church.org",
      otpPreference: "phone",
      password: "hashedPassword50",
      role: "member",
      lastSeenOffsetDays: 420, // Deceased member
      raCount: 0,
      status: "Inactive",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: false,
      preRaWarningSentOffsetDays: null,
      globalLock: true,
      lockStatus: "Deceased",
      deceasedInfo: {
        dateOfDeath: new Date("2023-05-15"),
        reason: "Car accident",
        memorialServiceDate: new Date("2023-05-25")
      },
      attendance: [
        {
          dateOffsetDays: 420,
          branch: "Bulawayo",
          type: "Regular",
          timestampOffsetHours: 240,
          markedBy: "admin002"
        }
      ],
      selfManagementData: {
        messages: ["In loving memory of Sipho"],
        attendanceSummary: {
          totalAttendances: 68,
          last90Days: 0,
          raCount: 0
        }
      }
    },
    {
      id: "mem051",
      names: ["Nomsa"],
      surname: "Dlamini",
      cardNumber: 1051,
      dob: new Date("1993-09-05"),
      virginityStatus: "non-virgin",
      dateOfEntry: new Date("2019-08-12"),
      reasonOfEntry: "Marriage restoration",
      gender: "female",
      position: "Steward",
      mainBranch: "Gaborone",
      phone: "+27823456751",
      email: "nomsa.dlamini@church.org",
      otpPreference: "email",
      password: "hashedPassword51",
      role: "member",
      lastSeenOffsetDays: 8,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 8,
          branch: "Gaborone",
          type: "Regular",
          timestampOffsetHours: 150,
          markedBy: "admin003"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Nomsa!"],
        attendanceSummary: {
          totalAttendances: 35,
          last90Days: 9,
          raCount: 0
        }
      }
    },
    {
      id: "mem052",
      names: ["Thabo"],
      surname: "Mkhwanazi",
      cardNumber: 1052,
      dob: new Date("2001-12-18"),
      virginityStatus: "virgin",
      dateOfEntry: new Date("2022-04-05"),
      reasonOfEntry: "Youth spiritual development",
      gender: "male",
      position: "Member",
      mainBranch: "Harare",
      phone: "+27823456752",
      email: "thabo.mkhwanazi@church.org",
      otpPreference: "phone",
      password: "hashedPassword52",
      role: "member",
      lastSeenOffsetDays: 3,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 3,
          branch: "Harare",
          type: "Regular",
          timestampOffsetHours: 120,
          markedBy: "admin004"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Thabo!"],
        attendanceSummary: {
          totalAttendances: 14,
          last90Days: 7,
          raCount: 0
        }
      }
    },
    {
      id: "mem053",
      names: ["Nolwazi"],
      surname: "Zwane",
      cardNumber: 1053,
      dob: new Date("1985-04-30"),
      virginityStatus: "inapplicable",
      dateOfEntry: new Date("2016-10-15"),
      reasonOfEntry: "Freedom from financial curses",
      gender: "female",
      position: "Holy Messenger",
      mainBranch: "Ireland",
      phone: "+27823456753",
      email: "nolwazi.zwane@church.org",
      otpPreference: "email",
      password: "hashedPassword53",
      role: "member",
      lastSeenOffsetDays: 25,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 25,
          branch: "Ireland",
          type: "Regular",
          timestampOffsetHours: 200,
          markedBy: "admin005"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Nolwazi!"],
        attendanceSummary: {
          totalAttendances: 52,
          last90Days: 11,
          raCount: 0
        }
      }
    },
    {
      id: "mem054",
      names: ["Mandla"],
      surname: "Ndlovu",
      cardNumber: 1054,
      dob: new Date("1978-11-12"),
      virginityStatus: "inapplicable",
      dateOfEntry: new Date("2015-03-22"),
      reasonOfEntry: "Deliverance from ancestral curses",
      gender: "male",
      position: "Evangelist",
      mainBranch: "Johannesburg",
      phone: "+27823456754",
      email: "mandla.ndlovu@church.org",
      otpPreference: "phone",
      password: "hashedPassword54",
      role: "admin",
      lastSeenOffsetDays: 10,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 10,
          branch: "Johannesburg",
          type: "Regular",
          timestampOffsetHours: 170,
          markedBy: "admin006"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Mandla!"],
        attendanceSummary: {
          totalAttendances: 75,
          last90Days: 15,
          raCount: 0
        }
      }
    },
    {
      id: "mem055",
      names: ["Zanele"],
      surname: "Mabaso",
      cardNumber: 1055,
      dob: new Date("1996-02-28"),
      virginityStatus: "non-virgin",
      dateOfEntry: new Date("2020-07-18"),
      reasonOfEntry: "Spiritual guidance",
      gender: "female",
      position: "Songster",
      mainBranch: "Bulawayo",
      phone: "+27823456755",
      email: "zanele.mabaso@church.org",
      otpPreference: "email",
      password: "hashedPassword55",
      role: "member",
      lastSeenOffsetDays: 7,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 7,
          branch: "Bulawayo",
          type: "Regular",
          timestampOffsetHours: 140,
          markedBy: "admin007"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Zanele!"],
        attendanceSummary: {
          totalAttendances: 28,
          last90Days: 8,
          raCount: 0
        }
      }
    },
    {
      id: "mem056",
      names: ["Bonginkosi"],
      surname: "Mhlongo",
      cardNumber: 1056,
      dob: new Date("2004-06-15"),
      virginityStatus: "virgin",
      dateOfEntry: new Date("2023-01-10"),
      reasonOfEntry: "Youth revival",
      gender: "male",
      position: "Member",
      mainBranch: "Gaborone",
      phone: "+27823456756",
      email: "bonginkosi.mhlongo@church.org",
      otpPreference: "phone",
      password: "hashedPassword56",
      role: "member",
      lastSeenOffsetDays: 2,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 2,
          branch: "Gaborone",
          type: "Regular",
          timestampOffsetHours: 90,
          markedBy: "admin008"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Bonginkosi!"],
        attendanceSummary: {
          totalAttendances: 9,
          last90Days: 6,
          raCount: 0
        }
      }
    },
    {
      id: "mem057",
      names: ["Thandeka"],
      surname: "Zulu",
      cardNumber: 1057,
      dob: new Date("1983-08-25"),
      virginityStatus: "inapplicable",
      dateOfEntry: new Date("2017-12-05"),
      reasonOfEntry: "Healing from chronic illness",
      gender: "female",
      position: "Facilitator",
      mainBranch: "Harare",
      phone: "+27823456757",
      email: "thandeka.zulu@church.org",
      otpPreference: "email",
      password: "hashedPassword57",
      role: "member",
      lastSeenOffsetDays: 12,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 12,
          branch: "Harare",
          type: "Regular",
          timestampOffsetHours: 190,
          markedBy: "admin009"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Thandeka!"],
        attendanceSummary: {
          totalAttendances: 46,
          last90Days: 10,
          raCount: 0
        }
      }
    },
    {
      id: "mem058",
      names: ["Sibusiso"],
      surname: "Nkosi",
      cardNumber: 1058,
      dob: new Date("1969-05-08"),
      virginityStatus: "inapplicable",
      dateOfEntry: new Date("2014-09-20"),
      reasonOfEntry: "Deliverance from witchcraft",
      gender: "male",
      position: "Conciliator",
      mainBranch: "Ireland",
      phone: "+27823456758",
      email: "sibusiso.nkosi@church.org",
      otpPreference: "phone",
      password: "hashedPassword58",
      role: "member",
      lastSeenOffsetDays: 500, // Deceased member
      raCount: 0,
      status: "Inactive",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: false,
      preRaWarningSentOffsetDays: null,
      globalLock: true,
      lockStatus: "Deceased",
      deceasedInfo: {
        dateOfDeath: new Date("2022-12-10"),
        reason: "Natural causes",
        memorialServiceDate: new Date("2022-12-20")
      },
      attendance: [
        {
          dateOffsetDays: 500,
          branch: "Ireland",
          type: "Regular",
          timestampOffsetHours: 300,
          markedBy: "admin010"
        }
      ],
      selfManagementData: {
        messages: ["In loving memory of Sibusiso"],
        attendanceSummary: {
          totalAttendances: 82,
          last90Days: 0,
          raCount: 0
        }
      }
    },
    {
      id: "mem059",
      names: ["Nomvula"],
      surname: "Mthembu",
      cardNumber: 1059,
      dob: new Date("1998-01-15"),
      virginityStatus: "virgin",
      dateOfEntry: new Date("2021-05-30"),
      reasonOfEntry: "Spiritual calling",
      gender: "female",
      position: "Holy Messenger",
      mainBranch: "Johannesburg",
      phone: "+27823456759",
      email: "nomvula.mthembu@church.org",
      otpPreference: "email",
      password: "hashedPassword59",
      role: "member",
      lastSeenOffsetDays: 5,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 5,
          branch: "Johannesburg",
          type: "Regular",
          timestampOffsetHours: 130,
          markedBy: "admin011"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Nomvula!"],
        attendanceSummary: {
          totalAttendances: 24,
          last90Days: 7,
          raCount: 0
        }
      }
    },
    {
      id: "mem060",
      names: ["Mthunzi"],
      surname: "Dlamini",
      cardNumber: 1060,
      dob: new Date("1991-10-22"),
      virginityStatus: "non-virgin",
      dateOfEntry: new Date("2018-08-12"),
      reasonOfEntry: "Marriage restoration",
      gender: "male",
      position: "Steward",
      mainBranch: "Bulawayo",
      phone: "+27823456760",
      email: "mthunzi.dlamini@church.org",
      otpPreference: "phone",
      password: "hashedPassword60",
      role: "member",
      lastSeenOffsetDays: 9,
      raCount: 0,
      status: "Active",
      reAdmissionOffsetDays: null,
      raHistory: [],
      trackingActive: true,
      preRaWarningSentOffsetDays: null,
      globalLock: false,
      lockStatus: "Unlocked",
      attendance: [
        {
          dateOffsetDays: 9,
          branch: "Bulawayo",
          type: "Regular",
          timestampOffsetHours: 160,
          markedBy: "admin012"
        }
      ],
      selfManagementData: {
        messages: ["Welcome Mthunzi!"],
        attendanceSummary: {
          totalAttendances: 38,
          last90Days: 9,
          raCount: 0
        }
      }
    }
  ]
};
