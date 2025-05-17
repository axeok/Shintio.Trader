<template>
	<div class="pa-4">
		{{ data.Pair }}
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

	const dates = $computed(() => {
		const startDate = new Date(data.StartTime);
		const endDate = new Date(data.EndTime);
		const stepSeconds = data.SaveStepSeconds;

		const stepMilliseconds = stepSeconds * 1000;
		const result = [];

		for (let time = startDate.getTime(); time <= endDate.getTime(); time += stepMilliseconds) {
			result.push(new Date(time));
		}

		return result;
	});

	// const prices = $computed(() => ({
	// 	name: "Price",
	// 	type: "candlestick",
	// 	data: data.Prices.map((value, i) => ({
	// 		x: dates[i],
	// 		// x: i,
	// 		y: value,
	// 	})),
	// }));

	const balances = $computed(() => (Object.entries(data.Values)
		.sort((a, b) => {
			const aLastValue = a[1][a[1].length - 1] ?? 0;
			const bLastValue = b[1][b[1].length - 1] ?? 0;
			return bLastValue - aLastValue;
		})
		.map(entry => ({
			name: entry[0],
			type: "line",
			data: entry[1].map((value, i) => ({
				x: dates[i],
				y: value,
			})),
		}))));

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
			// prices,
			...balances,
			// starts,
			// ends,
			// deltaBalances,
			// winratesCount,
			// winratesSum,
		];
	});
</script>
