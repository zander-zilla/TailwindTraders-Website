import axios from "axios";

// All runtime settings come from the backend (/api/settings); nothing is baked in at build time.
const settingsUrl = "/api/settings";

const ConfigService = {
  _needLoadSettings: true,
  _loading: null,
  _apiUrl: undefined,
  _apiUrlShoppingCart: undefined,
  _debugInformation: {},
  _browserTelemetry: undefined,

  async loadSettings() {
    if (!this._needLoadSettings) return;
    // Several components call loadSettings() on mount; share one request.
    this._loading = this._loading || axios.get(settingsUrl);
    const { data } = await this._loading;
    this._apiUrl = data.apiUrl;
    this._apiUrlShoppingCart = data.apiUrlShoppingCart;
    this._debugInformation = data.debugInformation || {};
    this._browserTelemetry = data.browserTelemetry;
    this._needLoadSettings = false;
  },

  HeadersConfig(token = undefined) {
    return { headers: token ? { Authorization: `Bearer ${token}` } : {} };
  },
};

export default ConfigService;
