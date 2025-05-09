<template>
	<div class="pa-4">
		<apexchart
			width="100%"
			height="800"
			:options="options"
			:series="series"
		/>
	</div>
</template>

<script setup>
	import data from "../../../Shintio.Trader/bin/Debug/net9.0/benchmark.json";

	const labels = $computed(() => {
		const startDate = new Date(data.StartTime);
		const endDate = new Date(data.EndTime);
		const stepSeconds = data.SaveStepSeconds;

		const stepMilliseconds = stepSeconds * 1000;
		const dates = [];

		for (let time = startDate.getTime(); time <= endDate.getTime(); time += stepMilliseconds) {
			dates.push(new Date(time));
		}

		return dates.map(date => {
			const yyyy = date.getFullYear();
			const mm = String(date.getMonth() + 1).padStart(2, "0");
			const dd = String(date.getDate()).padStart(2, "0");

			return `${yyyy}-${mm}-${dd}`;
		});
	});

	const prices = $computed(() => {
		return data.Prices.map(p => {
			return {
				name: p.Pair,
				type: "candlestick",
				data: p.Values.map((value, i) => ({
					x: new Date(value.Time),
					y: value.Value,
				})),
				// name: "Price",
			};
		});
	});

	const strategies = $computed(() => {
		return data.Strategies.sort((a, b) => {
			const aLastValue = a.Values[a.Values.length - 1]?.Value || 0;
			const bLastValue = b.Values[b.Values.length - 1]?.Value || 0;
			return bLastValue - aLastValue;
		});

	});

	const totalBalances = $computed(() => {
		return strategies.map((strategy, i) => {
			const index = i + 1;

			return {
				// name: `#${index} ${strategy.Name}`,
				type: "line",
				data: strategy.Values.map((value, i) => ({
					x: new Date(value.Time),
					y: value.Value,
				})),
				name: "Total balance",
				color: "white",
			};
		});
	});

	const balances = $computed(() => {
		return strategies.map((strategy, i) => {
			const index = i + 1;

			return {
				// name: `BALANCE #${index} ${strategy.Name}`,
				type: "area",
				data: strategy.Balances.map((value, i) => ({
					x: new Date(value.Time),
					y: value.Value,
				})),
				name: "Balance",
				color: "#4E8AEE",
				fill: {
					gradient: {
						enabled: true,
						opacityFrom: 0.55,
						opacityTo: 0
					}
				},
			};
		});
	});

	const shorts = $computed(() => {
		return strategies.map((strategy, i) => {
			const index = i + 1;

			return {
				// name: `SHORTS #${index} ${strategy.Name}`,
				type: "bar",
				data: strategy.Shorts.map((value, i) => ({
					x: new Date(value.Time),
					y: value.Value,
				})),
				name: "Shorts",
				color: "rgb(239, 64, 60)",
			};
		});
	});

	const longs = $computed(() => {
		return strategies.map((strategy, i) => {
			const index = i + 1;

			return {
				// name: `LONGS #${index} ${strategy.Name}`,
				type: "bar",
				data: strategy.Longs.map((value, i) => ({
					x: new Date(value.Time),
					y: value.Value,
				})),
				name: "Longs",
				color: "rgb(0, 183, 70)",
			};
		});
	});

	const shortsCount = $computed(() => {
		return strategies.map((strategy, i) => {
			const index = i + 1;

			return {
				// name: `SHORTS Count #${index} ${strategy.Name}`,
				type: "area",
				data: strategy.ShortsCount.map((value, i) => ({
					x: new Date(value.Time),
					y: value.Value,
				})),
				name: "Shorts count",
				color: "rgb(239, 64, 60)",
			};
		});
	});

	const longsCount = $computed(() => {
		return strategies.map((strategy, i) => {
			const index = i + 1;

			return {
				// name: `LONGS Count #${index} ${strategy.Name}`,
				type: "area",
				data: strategy.LongsCount.map((value, i) => ({
					x: new Date(value.Time),
					y: value.Value,
				})),
				name: "Longs count",
				color: "rgb(0, 183, 70)",
			};
		});
	});

	const options = $ref({
		chart: {
			id: "vuechart-example",
		},
		legend: {
			position: "top",
			horizontalAlign: "left",
		},
		tooltip: {
			enabled: true,
			shared: true,
			intersect: false,
			onDatasetHover: {
				highlightDataSeries: true,
			},
			x: {
				show: true,
			},
		},
		xaxis: {
			type: "datetime",
		},
		yaxis: [
			{
				decimalsInFloat: 2,
				seriesName: ["Price"],
				title: {
					text: "Price",
				},
			},
			{
				decimalsInFloat: 0,
				seriesName: ["Balance", "Total balance"],
				title: {
					text: "Balance",
				},
				opposite: true,
			},
			{
				decimalsInFloat: 0,
				seriesName: [],
				title: {
					text: "SHORTS/LONGS",
				},
			},
			{
				decimalsInFloat: 0,
				seriesName: ["Shorts count", "Longs count"],
				title: {
					text: "SHORTS/LONGS COUNT",
				},
			},
		],
	});

	const series = $computed(() => {
		return [
			...prices,
			...totalBalances,
			...balances,
			...shorts,
			...longs,
			...shortsCount,
			...longsCount,
		];
	});
</script>
