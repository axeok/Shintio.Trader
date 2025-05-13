import { defineStore } from "pinia";
import { usePreferredDark } from "@vueuse/core/index";

const isDark = usePreferredDark();

export const useAppStore = defineStore("app", {
	state: () => ({
		config: {},
		isDark: isDark,
		window: {
			width: 0,
			height: 0,
		},
	}),
});
