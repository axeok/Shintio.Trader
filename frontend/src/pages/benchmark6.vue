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

	const values = $computed(() => Object.values(data.Values));
	
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

	const balance = $computed(() => ({
		name: "Balance",
		type: "line",
		data: values.map((value, i) => ({
			x: dates[i],
			y: value[0],
		})),
	}));

	const totalBalance = $computed(() => ({
		name: "TotalBalance",
		type: "line",
		data: values.map((value, i) => ({
			x: dates[i],
			y: value[1],
		})),
	}));

	const orders = $computed(() => ({
		name: "Orders",
		type: "line",
		data: values.map((value, i) => ({
			x: dates[i],
			y: value[2],
		})),
	}));

	const pairsBalances = $computed(() => {
		let pairIndex = 2;

		return data.Pairs.map(pair => {
			pairIndex++;

			return {
				name: pair,
				type: "line",
				data: values.map((value, i) => ({
					x: dates[i],
					y: value[pairIndex],
				})),
			};
		});
	});

	const totalPnls = $computed(() => {
		let pairIndex = -1;

		return data.Pairs.map(pair => {
			pairIndex++;

			return {
				name: `Total PnL ${pair}`,
				type: "line",
				data: data.TotalPnls.map((value, i) => ({
					x: dates[i],
					y: value[pairIndex],
				})),
			};
		});
	});

	const price = $computed(() => ({
		name: "Price",
		type: "line",
		data: data.Prices.map((value, i) => ({
			x: dates[i],
			y: value,
		})),
	}));

	const options = $computed(() => ({
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
				format: 'dd MMM yyyy HH:mm',
			},
		},
		xaxis: {
			type: "datetime",
		},
		yaxis: [
			{
				decimalsInFloat: 0,
				seriesName: ["TotalBalance", "Balance", "Orders"],
				title: {
					text: "Balance",
				},
				opposite: false,
			},
			// {
			// 	decimalsInFloat: 0,
			// 	seriesName: ["Balance"],
			// 	title: {
			// 		text: "Balance",
			// 	},
			// 	opposite: true,
			// },
			{
				decimalsInFloat: 0,
				seriesName: pairsBalances.map(p => p.name),
				title: {
					text: "PnL",
				},
				opposite: true,
			},
			{
				decimalsInFloat: 0,
				seriesName: totalPnls.map(p => p.name),
				title: {
					text: "Total PnL",
				},
				opposite: true,
			},
			// {
			// 	decimalsInFloat: 0,
			// 	seriesName: "Price",
			// 	title: {
			// 		text: "Price",
			// 	},
			// 	opposite: true,
			// },
		],
	}));

	const series = $computed(() => {
		return [
			// prices,
			totalBalance,
			balance,
			orders,
			...pairsBalances,
			...totalPnls,
			// price,
			// starts,
			// ends,
			// deltaBalances,
			// winratesCount,
			// winratesSum,
		];
	});
</script>
