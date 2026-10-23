export interface RootResponse {
  service: string;
  status: string;
  version: string;
}

export interface ApiError {
  code?: string;
  message: string;
}

export class ApiClient {
  constructor(private readonly baseUrl: string) {}

  async getRoot(): Promise<RootResponse> {
    const response = await fetch(`${this.baseUrl}/`);
    if (!response.ok) {
      throw new Error(
        `GET / returned HTTP ${response.status} ${response.statusText}`,
      );
    }
    return (await response.json()) as RootResponse;
  }
}
