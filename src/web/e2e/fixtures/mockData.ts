import type { Page } from '@playwright/test'

export const mockSchoolSearch = [
  {
    id: 1001,
    name: 'The University of Texas at Austin',
    city: 'Austin',
    state: 'TX',
  },
]

export const mockSchoolDetail = {
  id: 1001,
  name: 'The University of Texas at Austin',
  city: 'Austin',
  state: 'TX',
  schoolUrl: 'www.utexas.edu',
  ownership: 1,
  ownershipName: 'Public',
  admissionRate: 0.31,
  studentSize: 40000,
  tuitionInState: 11752,
  tuitionOutOfState: 40996,
  completionRate: 0.88,
  programs: [
    {
      code: '11.0701',
      title: 'Computer Science',
      credentialLevel: 3,
      credentialName: "Bachelor's Degree",
      completions: 350,
      medianEarnings: 95000,
    },
    {
      code: '14.0101',
      title: 'Engineering, General',
      credentialLevel: 3,
      credentialName: "Bachelor's Degree",
      completions: 120,
      medianEarnings: 82000,
    },
    {
      code: '09.0101',
      title: 'Communication, General',
      credentialLevel: 3,
      credentialName: "Bachelor's Degree",
      completions: 45,
      medianEarnings: null,
    },
  ],
}

export const mockTuitionTrend = [
  { year: 2020, inState: 11100, outOfState: 39500 },
  { year: 2021, inState: 11448, outOfState: 40032 },
  { year: 2022, inState: 11752, outOfState: 40996 },
]

export const mockLocationSearch = [
  {
    cbsaCode: '12420',
    name: 'Austin-Round Rock-Georgetown',
    state: 'TX',
    type: 'Metro',
    oneBedRent: 1550,
    twoBedRent: 1950,
    hasHousingData: true,
  },
  {
    cbsaCode: '99999',
    name: 'Rural Outpost Without Housing Data',
    state: 'TX',
    type: 'Micro',
    oneBedRent: null,
    twoBedRent: null,
    hasHousingData: false,
  },
]

export const mockFinanceSimulation = {
  grossAnnualSalary: 95000,
  grossMonthly: 7917,
  netMonthly: 6150,
  effectiveTaxRate: 0.223,
  salarySource: 'scorecard_median',
  rentMonthly: 1550,
  housingType: '1bed',
  loanPaymentMonthly: 450,
  loanPrincipal: 47008,
  fixedCostsMonthly: 2000,
  disposableMonthly: 4150,
  incomeStatus: 'comfortable',
  locationName: 'Austin-Round Rock-Georgetown',
  state: 'TX',
}

export const mockJobPulse = {
  cipCode: '11.0701',
  cbsaCode: '12420',
  searchKeywords: 'Computer Science',
  activeOpenings: 1420,
  localMedianSalary: 102000,
  scorecardMedianEarnings: 95000,
  dataSource: 'adzuna',
  locationName: 'Austin-Round Rock-Georgetown, TX',
}

export async function setupMockApi(page: Page) {
  await page.route('**/api/**', async (route) => {
    const url = new URL(route.request().url())

    if (url.pathname.endsWith('/tuition-trend')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(mockTuitionTrend),
      })
    }

    if (url.pathname === '/api/schools/search') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(mockSchoolSearch),
      })
    }

    if (url.pathname.startsWith('/api/schools/')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(mockSchoolDetail),
      })
    }

    if (url.pathname === '/api/locations/search') {
      const requireHousing = url.searchParams.get('requireHousing') === 'true'
      const locations = requireHousing
        ? mockLocationSearch.filter((l) => l.hasHousingData)
        : mockLocationSearch
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(locations),
      })
    }

    if (url.pathname === '/api/jobs/pulse') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(mockJobPulse),
      })
    }

    if (url.pathname === '/api/finance/simulator') {
      const postData = route.request().postDataJSON()
      if (postData?.salaryOverride) {
        const overrideVal = postData.salaryOverride
        const monthly = Math.round(overrideVal / 12)
        const net = Math.round(monthly * 0.75)
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            ...mockFinanceSimulation,
            grossAnnualSalary: overrideVal,
            grossMonthly: monthly,
            netMonthly: net,
            disposableMonthly: net - mockFinanceSimulation.fixedCostsMonthly,
            salarySource: 'user_override',
          }),
        })
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(mockFinanceSimulation),
      })
    }

    return route.continue()
  })
}

