/**
 * plugins/index.js
 *
 * Automatically included in `./src/main.js`
 */

// Plugins
import vuetify from "./vuetify";
import pinia from "./pinia";
import router from "@/router";
import VueApexCharts from "vue3-apexcharts";

window.Apex = {
	chart: {
		foreColor: "#ccc",
		toolbar: {
			show: true,
		},
		animations: {
			animateGradually: {
				enabled: false,
			},
		},
	},
	tooltip: {
		theme: "dark",
	},
	grid: {
		borderColor: "#535A6C",
		xaxis: {
			lines: {
				show: false,
			},
		},
	},
};

export function registerPlugins(app) {
	app
		.use(vuetify)
		.use(router)
		.use(pinia)
		.use(VueApexCharts);
}
