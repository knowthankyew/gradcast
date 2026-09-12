export interface SchoolSearchResult {
  id: number
  name: string
  city: string
  state: string
}

export interface ProgramData {
  code: string
  title: string
  credentialLevel: number
  credentialName: string
  completions: number | null
  medianEarnings: number | null
}

export interface SchoolDetail {
  id: number
  name: string
  city: string
  state: string
  schoolUrl: string | null
  ownership: number
  ownershipName: string
  admissionRate: number | null
  studentSize: number | null
  tuitionInState: number | null
  tuitionOutOfState: number | null
  completionRate: number | null
  programs: ProgramData[]
}

export interface ProgramCategory {
  code: string
  name: string
  programs: ProgramData[]
  totalCompletions: number
}

export interface BudgetSimulation {
  grossAnnualSalary: number
  grossMonthly: number
  netMonthly: number
  effectiveTaxRate: number
  salarySource: string
  hasReportedEarnings: boolean
  rentMonthly: number
  housingType: string
  loanPaymentMonthly: number
  loanPrincipal: number
  fixedCostsMonthly: number
  disposableMonthly: number
  incomeStatus: string
  locationName: string
  state: string
}
