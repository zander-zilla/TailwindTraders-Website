import React from "react";
import ReactDOM from "react-dom";
import { Provider } from 'react-redux';
import "./index.css";
import App from "./App";
import './i18n';
import store from './store';
import ConfigService from './services/configService';
import { startBrowserTelemetry } from './telemetry';

(async () => {
    await ConfigService.loadSettings();
    startBrowserTelemetry(ConfigService._browserTelemetry);

    ReactDOM.render(
        <Provider store={store}><App /></Provider>,
        document.getElementById('root')
    );
})();