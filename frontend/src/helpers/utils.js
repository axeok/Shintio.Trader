export const $utils = {
	sum: function (array, lambda) {
		return array.reduce((partialSum, a) => partialSum + lambda(a), 0);
	},
	random: function (min, max) {
		return Math.random() * (max - min) + min;
	},
	randomInt: function (min, max) {
		return Math.round(this.random(min, max));
	},
	random01: function () {
		return Math.random();
	},
	randomValue: function (items) {
		return items[Math.floor(Math.random() * items.length)];
	},
};