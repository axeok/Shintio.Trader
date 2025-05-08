export const $formatter = {
	asPercent(value, round = false, digits = 2) {
		return this.asNumber((round ? Math.round(value * 100) : value * 100), digits) + "%";
	},
	asNumber(value, digits = 2) {
		return parseFloat(value).toLocaleString(locale, {
			minimumFractionDigits: digits,
			maximumFractionDigits: digits,
		});
	},
	asMoney(money) {
		let suffix = "";

		if (money >= 1000000000) {
			suffix = "K";
			money /= 1000;
		}
		if (money >= 1000000000) {
			suffix = "M";
			money /= 1000;
		}

		return `${parseFloat(money).toLocaleString("en", {
			minimumFractionDigits: 0,
			maximumFractionDigits: 2,
		}).replaceAll(",", " ")}${suffix}`;
	},
};