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

	const prices = $computed(() => ({
		name: "Price",
		type: "candlestick",
		data: data.Prices.map((value, i) => ({
			x: dates[i],
			// x: i,
			y: value,
		})),
	}));

	const balances = $computed(() => ({
		name: "Balance",
		type: "line",
		data: data.Values.map((value, i) => ({
			x: dates[i],
			y: value,
		})),
	}));

	const starts = $computed(() => ({
		name: "Start",
		type: "line",
		data: data.Starts.map((value, i) => ({
			x: dates[i],
			y: value,
		})),
	}));

	const ends = $computed(() => ({
		name: "End",
		type: "line",
		data: data.Ends.map((value, i) => ({
			x: dates[i],
			y: value,
		})),
	}));

	const deltaBalances = $computed(() => ({
		name: "Delta Balance",
		type: "line",
		data: data.DeltaBalances.map((value, i) => ({
			x: dates[i],
			y: value,
		})),
	}));

	const winratesCount = $computed(() => ({
		name: "Winrate (Count)",
		type: "line",
		data: data.WinratesCount.map((value, i) => ({
			x: dates[i],
			y: value,
		})),
	}));

	const winratesSum = $computed(() => ({
		name: "Winrate (Sum)",
		type: "line",
		data: data.WinratesSum.map((value, i) => ({
			x: dates[i],
			y: value,
		})),
	}));

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
				seriesName: ["Balance", "Delta Balance"],
				title: {
					text: "Balance",
				},
				opposite: true,
			},
			{
				decimalsInFloat: 3,
				seriesName: ["Start", "End"],
				title: {
					text: "Start/End",
				},
			},
			// {
			// 	decimalsInFloat: 4,
			// 	seriesName: ["Winrate (Count)", "Winrate (Sum)"],
			// 	title: {
			// 		text: "Winrate",
			// 	},
			// },
		],
	});

	const series = $computed(() => {
		return [
			// prices,
			balances,
			// starts,
			// ends,
			// deltaBalances,
			// winratesCount,
			// winratesSum,
		];
	});
</script>
