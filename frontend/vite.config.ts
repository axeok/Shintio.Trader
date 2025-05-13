// Plugins
import AutoImport from "unplugin-auto-import/vite";
import Components from "unplugin-vue-components/vite";
import Fonts from "unplugin-fonts/vite";
import Layouts from "vite-plugin-vue-layouts-next";
import Vue from "@vitejs/plugin-vue";
import Pages from "vite-plugin-pages";
import Vuetify, {transformAssetUrls} from "vite-plugin-vuetify";
import VueMacros from "unplugin-vue-macros/vite";
import autoprefixer from "autoprefixer";

// Utilities
import {defineConfig} from "vite";
import path from "path";

// https://vitejs.dev/config/
export default defineConfig({
	plugins: [
		Pages(),
		Layouts(),
		Components(),
		VueMacros({
			plugins: {
				vue: Vue({
					template: {
						transformAssetUrls,
					},
					include: [/\.vue$/],
				}),
			},
		}),
		Vuetify({
			autoImport: true,
			styles: {
				configFile: "src/assets/styles/_vuetify.scss",
			},
		}),
		Fonts({
			google: {
				families: [],
			},
		}),
		AutoImport({
			imports: [
				"vue",
				"vue-router",
				"vue/macros",
				"@vueuse/core",
				"pinia",
				{
					"vuetify": [
						"useTheme",
					],
				},
			],
			dts: "./src/auto-imports.d.ts",
			eslintrc: {
				enabled: true,
			},
			vueTemplate: true,
			dirs: [
				"src/composables",
				"src/store",
				"src/helpers",
			],
		}),
	],
	define: {"process.env": {}},
	resolve: {
		alias: {
			"@": path.resolve(__dirname, "src"),
			"~": path.resolve(__dirname, "src"),
		},
		extensions: [".js", ".json", ".jsx", ".mjs", ".ts", ".tsx", ".vue"],
	},
	css: {
		postcss: {
			plugins: [
				autoprefixer(),
			],
		},
	},
	server: {
		port: 8080,
		open: "/",
		warmup: {
			clientFiles: [
				"./src/pages/index.vue",
			],
		},
	},
});
