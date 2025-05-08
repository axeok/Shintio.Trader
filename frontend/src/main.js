/**
 * main.js
 *
 * Bootstraps Vuetify and other plugins then mounts the App`
 */

// Plugins
import { registerPlugins } from "@/plugins";

// Components
import App from "./App.vue";

// Composables
import { createApp } from "vue";
import { $formatter } from "@/helpers/formatter";
import { $utils } from "@/helpers/utils";

const app = createApp(App);

app.config.globalProperties.$formatter = $formatter;
app.config.globalProperties.$utils = $utils;

registerPlugins(app);

app.mount("#app");
