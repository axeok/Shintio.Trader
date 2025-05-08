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
	import data
		from "C:/Users/shint/RiderProjects/shintio/Shintio.Trader/Shintio.Trader/bin/Debug/net9.0/benchmark.json";

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
		return Object.entries(data.Prices).map(e => {
			return {
				name: e[0],
				type: "candlestick",
				data: e[1].map((value, i) => ({
					x: i,
					y: value,
				})),
				yAxisIndex: 0,
			};
		});
	});

	const strategies = $computed(() => {
		return data.Strategies.map((strategy, i) => {
			const index = i + 1;

			return {
				name: `#${index} ${strategy.Name}`,
				type: "line",
				data: strategy.Values.map((value, i) => ({
					x: i,
					y: value,
				})),
				yAxisIndex: 1,
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
			categories: labels,
		},
		yaxis: [
			{
				decimalsInFloat: 2,
				title: {
					text: "Price",
				},
			},
			{
				decimalsInFloat: 0,
				title: {
					text: "Balance",
				},
				opposite: true,
			},
		],

	});

	const series = $computed(() => {
		return [
			...prices,
			...strategies,
		];
	});
</script>
