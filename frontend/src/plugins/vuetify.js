import "vuetify/styles";
import "../assets/styles/index.scss";
import "../assets/fonts/FixelVariable/style.css";
import { en, ru } from "vuetify/locale";
import { aliases, mdi } from "vuetify/iconsets/mdi";
import * as labs from "vuetify/labs/components";

import { createVuetify } from "vuetify";

export default createVuetify({
	components: {
		...labs,
	},
	theme: {
		defaultTheme: "themeDark",
		themes: {
			themeDark: {
				dark: true,
				colors: {
					background: "#020202",
					surface: "#0C0C0C",
					primary: "#6F51E3",
				},
				variables: {
					"border-color": "#FFFFFF",
					"border-opacity": "0.1",
					"btn-height": "40px",
					"theme-gradient-direction": "90deg",
					"theme-primary-gradient": "#4E8AEE",
				},
			},
		},
	},
	locale: {
		locale: "ru",
		fallback: "en",
		messages: { ru, en },
	},
	icons: {
		defaultSet: "mdi",
		aliases,
		sets: {
			mdi,
		},
	},
});
